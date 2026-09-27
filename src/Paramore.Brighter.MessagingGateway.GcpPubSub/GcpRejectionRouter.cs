#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// Internal collaborator that routes a rejected GCP Pub/Sub message to its dead-letter or
/// invalid-message destination, stamping Brighter rejection metadata on the copy before publishing.
/// </summary>
/// <remarks>
/// One instance per consumer; not thread-safe (each performer has its own consumer, so
/// <c>Reject</c> is never called concurrently on one instance).
/// The router never throws: every broker and producer call is caught and turned into a
/// <see cref="RoutingOutcome"/> plus a log line.
/// </remarks>
internal sealed class GcpRejectionRouter : IDisposable, IAsyncDisposable
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<GcpRejectionRouter>();

    private readonly GcpMessagingGatewayConnection _connection;
    private readonly RoutingKey? _deadLetterRoutingKey;
    private readonly RoutingKey? _invalidMessageRoutingKey;
    private readonly OnMissingChannel _makeChannels;
    private readonly string _projectId;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Cached producer — <see langword="null"/> means not yet created or the last creation failed.
    /// Only success is cached: a <see cref="RoutingOutcome.Failed"/> outcome disposes and clears this.
    /// </summary>
    private IAmAMessageProducer? _producer;

    /// <summary>
    /// Initialises a new <see cref="GcpRejectionRouter"/>.
    /// </summary>
    /// <param name="connection">The GCP connection used to build the destination producer.</param>
    /// <param name="deadLetterRoutingKey">
    /// The Brighter-managed dead-letter topic. <see langword="null"/> disables DLQ routing.
    /// </param>
    /// <param name="invalidMessageRoutingKey">
    /// The Brighter-managed invalid-message topic. <see langword="null"/> disables invalid-message
    /// routing, in which case an <see cref="RejectionReason.Unacceptable"/> reason falls back to the DLQ.
    /// </param>
    /// <param name="makeChannels">
    /// Inherited from the subscription. Controls whether the destination topic is created on first use.
    /// </param>
    /// <param name="projectId">
    /// The GCP project id for the destination producer, inherited from the subscription.
    /// </param>
    /// <param name="timeProvider">Used to stamp <c>rejectionTimestamp</c>.</param>
    internal GcpRejectionRouter(
        GcpMessagingGatewayConnection connection,
        RoutingKey? deadLetterRoutingKey,
        RoutingKey? invalidMessageRoutingKey,
        OnMissingChannel makeChannels,
        string projectId,
        TimeProvider timeProvider)
    {
        _connection = connection;
        _deadLetterRoutingKey = deadLetterRoutingKey;
        _invalidMessageRoutingKey = invalidMessageRoutingKey;
        _makeChannels = makeChannels;
        _projectId = projectId;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Synchronously routes <paramref name="message"/> to the appropriate dead-letter destination
    /// based on <paramref name="reason"/>, stamping rejection metadata in place.
    /// </summary>
    /// <param name="message">
    /// The message to route. The caller must have already copied the receipt handle before calling
    /// this method; the router removes <c>ReceiptHandle</c> from <see cref="MessageHeader.Bag"/>.
    /// </param>
    /// <param name="reason">The rejection reason, or <see langword="null"/>.</param>
    /// <returns>
    /// <see cref="RoutingOutcome.NoDestination"/> when no routing key matches,
    /// <see cref="RoutingOutcome.Routed"/> on a successful publish,
    /// <see cref="RoutingOutcome.Failed"/> when producer creation or the publish threw.
    /// </returns>
    internal RoutingOutcome Route(Message message, MessageRejectionReason? reason)
    {
        var destination = ChooseDestination(reason);
        if (destination == null)
        {
            return RoutingOutcome.NoDestination;
        }

        StampMetadata(message, reason, destination, _timeProvider);

        try
        {
            if (_producer == null)
            {
                _producer = CreateProducer(destination);
            }

            ((IAmAMessageProducerSync)_producer).Send(message);
            return RoutingOutcome.Routed;
        }
        catch (Exception ex)
        {
            s_logger.LogError(ex,
                "GcpRejectionRouter: failed to route message {Id} with reason {Reason} to destination {Destination}",
                message.Id.Value,
                reason?.RejectionReason.ToString() ?? RejectionReason.None.ToString(),
                destination.Value);
            DisposeProducerSync();
            return RoutingOutcome.Failed;
        }
    }

    /// <summary>
    /// Asynchronously routes <paramref name="message"/> to the appropriate dead-letter destination
    /// based on <paramref name="reason"/>, stamping rejection metadata in place.
    /// </summary>
    /// <param name="message">
    /// The message to route. The caller must have already copied the receipt handle before calling
    /// this method; the router removes <c>ReceiptHandle</c> from <see cref="MessageHeader.Bag"/>.
    /// </param>
    /// <param name="reason">The rejection reason, or <see langword="null"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="RoutingOutcome.NoDestination"/> when no routing key matches,
    /// <see cref="RoutingOutcome.Routed"/> on a successful publish,
    /// <see cref="RoutingOutcome.Failed"/> when producer creation or the publish threw.
    /// </returns>
    internal async Task<RoutingOutcome> RouteAsync(Message message, MessageRejectionReason? reason, CancellationToken ct)
    {
        var destination = ChooseDestination(reason);
        if (destination == null)
        {
            return RoutingOutcome.NoDestination;
        }

        StampMetadata(message, reason, destination, _timeProvider);

        try
        {
            if (_producer == null)
            {
                _producer = await CreateProducerAsync(destination, ct);
            }

            await ((IAmAMessageProducerAsync)_producer).SendAsync(message, ct);
            return RoutingOutcome.Routed;
        }
        catch (Exception ex)
        {
            s_logger.LogError(ex,
                "GcpRejectionRouter: failed to route message {Id} with reason {Reason} to destination {Destination}",
                message.Id.Value,
                reason?.RejectionReason.ToString() ?? RejectionReason.None.ToString(),
                destination.Value);
            await DisposeProducerAsync();
            return RoutingOutcome.Failed;
        }
    }

    /// <summary>
    /// Chooses the destination routing key for the rejected message based on <paramref name="reason"/>.
    /// <see cref="RejectionReason.Unacceptable"/> routes to the invalid-message topic, falling back
    /// to the DLQ when no invalid-message topic is configured. Every other reason
    /// (<see cref="RejectionReason.DeliveryError"/>, <see cref="RejectionReason.None"/>, or no reason
    /// at all) routes to the DLQ. <see langword="null"/> when neither destination is configured.
    /// </summary>
    private RoutingKey? ChooseDestination(MessageRejectionReason? reason) =>
        reason?.RejectionReason == RejectionReason.Unacceptable
            ? _invalidMessageRoutingKey ?? _deadLetterRoutingKey
            : _deadLetterRoutingKey;

    /// <summary>
    /// Stamps Brighter rejection metadata onto <paramref name="message"/> in place.
    /// Removes the <c>ReceiptHandle</c> entry so the copy published to the destination does not
    /// carry the source subscription's ack handle. Sets <c>Header.Topic</c> to
    /// <paramref name="destination"/>.
    /// </summary>
    private static void StampMetadata(
        Message message,
        MessageRejectionReason? reason,
        RoutingKey destination,
        TimeProvider timeProvider)
    {
        message.Header.Bag[RejectionMetadataKeyNames.OriginalTopic] = message.Header.Topic.Value;
        message.Header.Bag[RejectionMetadataKeyNames.OriginalMessageType] = message.Header.MessageType.ToString();
        message.Header.Bag[RejectionMetadataKeyNames.RejectionTimestamp] = timeProvider.GetUtcNow().ToString("o");
        message.Header.Bag.Remove("ReceiptHandle");

        if (reason == null)
        {
            message.Header.Bag[RejectionMetadataKeyNames.RejectionReason] = RejectionReason.None.ToString();
            // No rejectionMessage when reason is null/None
        }
        else
        {
            message.Header.Bag[RejectionMetadataKeyNames.RejectionReason] = reason.RejectionReason.ToString();
            if (!string.IsNullOrEmpty(reason.Description))
            {
                message.Header.Bag[RejectionMetadataKeyNames.RejectionMessage] = reason.Description;
            }
        }

        message.Header.Topic = destination;
    }

    /// <summary>
    /// Creates the destination producer synchronously via <see cref="GcpPubSubMessageProducerFactory"/>.
    /// Throws on failure; the caller catches and turns the throw into a <see cref="RoutingOutcome.Failed"/>.
    /// </summary>
    private IAmAMessageProducer CreateProducer(RoutingKey destination)
    {
        var publication = BuildPublication(destination);
        var factory = new GcpPubSubMessageProducerFactory(_connection, [publication]);
        var producers = factory.Create();
        return producers.Values.First();
    }

    /// <summary>
    /// Creates the destination producer asynchronously via <see cref="GcpPubSubMessageProducerFactory"/>.
    /// Throws on failure; the caller catches and turns the throw into a <see cref="RoutingOutcome.Failed"/>.
    /// </summary>
    private async Task<IAmAMessageProducer> CreateProducerAsync(RoutingKey destination, CancellationToken ct)
    {
        var publication = BuildPublication(destination);
        var factory = new GcpPubSubMessageProducerFactory(_connection, [publication]);
        var producers = await factory.CreateAsync();
        return producers.Values.First();
    }

    /// <summary>
    /// Builds the <see cref="GcpPublication"/> for the destination topic, inheriting
    /// <c>MakeChannels</c> and project id from the subscription, and always enabling message ordering.
    /// </summary>
    private GcpPublication BuildPublication(RoutingKey destination) =>
        new GcpPublication
        {
            Topic = destination,
            MakeChannels = _makeChannels,
            TopicAttributes = new TopicAttributes { ProjectId = _projectId },
            EnableMessageOrdering = true,
        };

    private void DisposeProducerSync()
    {
        try
        {
            if (_producer is IDisposable d) d.Dispose();
        }
        catch (Exception ex)
        {
            s_logger.LogError(ex, "GcpRejectionRouter: error disposing producer after failed route");
        }
        finally
        {
            _producer = null;
        }
    }

    private async Task DisposeProducerAsync()
    {
        try
        {
            if (_producer is IAsyncDisposable ad)
                await ad.DisposeAsync();
            else if (_producer is IDisposable d)
                d.Dispose();
        }
        catch (Exception ex)
        {
            s_logger.LogError(ex, "GcpRejectionRouter: error disposing producer after failed route");
        }
        finally
        {
            _producer = null;
        }
    }

    /// <inheritdoc/>
    public void Dispose() => DisposeProducerSync();

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await DisposeProducerAsync();
    }
}
