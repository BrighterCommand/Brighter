#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using Paramore.Brighter.Tasks;
using Paramore.Brighter.Observability;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Paramore.Brighter.Logging;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

/// <summary>
/// Schedules retries directly on a queue using Azure Service Bus native scheduling.
/// </summary>
/// <remarks>
/// The destination queue must already exist in the client's namespace. No administration requests
/// are made. A retry gets a distinct, deterministic delivery ID and retains its first ID in
/// <see cref="Message.OriginalMessageIdHeaderName"/>. The caller owns the client and settles the
/// original delivery after scheduling succeeds. Each sender is closed after use.
/// </remarks>
public sealed partial class AzureServiceBusRequeueScheduler : IAmAMessageRequeueSchedulerAsync, IAmAMessageRequeueSchedulerSync
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<AzureServiceBusRequeueScheduler>();
    private readonly IServiceBusSenderProvider _senderProvider;
    private readonly TimeProvider _timeProvider;
    private readonly InstrumentationOptions _instrumentationOptions;

    /// <summary>Creates a native queue retry scheduler for the supplied client.</summary>
    /// <param name="clientProvider">The provider for the consuming broker's client.</param>
    /// <param name="timeProvider">The clock used to calculate delivery times; defaults to the system clock.</param>
    /// <param name="senderOptions">Options used when creating queue senders.</param>
    /// <param name="instrumentationOptions">The verbosity of retry telemetry.</param>
    public AzureServiceBusRequeueScheduler(IServiceBusClientProvider clientProvider,
        TimeProvider? timeProvider = null, ServiceBusSenderOptions? senderOptions = null,
        InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
        : this(new ServiceBusSenderProvider(clientProvider, senderOptions), timeProvider ?? TimeProvider.System, instrumentationOptions)
    {
    }

    internal AzureServiceBusRequeueScheduler(IServiceBusSenderProvider senderProvider, TimeProvider timeProvider,
        InstrumentationOptions instrumentationOptions)
    {
        _senderProvider = senderProvider;
        _timeProvider = timeProvider;
        _instrumentationOptions = instrumentationOptions;
    }

    /// <inheritdoc />
    public async Task RequeueAsync(Message message, ChannelName destination, TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));
        if (destination is null || !AzureServiceBusQueuePath.IsValid(destination.Value))
            throw new ArgumentException("A queue name in the scheduler's namespace is required.", nameof(destination));
        if (delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "The retry delay cannot be negative.");
        cancellationToken.ThrowIfCancellationRequested();

        var outgoing = CopyForRetry(message);
        var originalId = message.Header.Bag.TryGetValue(Message.OriginalMessageIdHeaderName, out var original)
            && !string.IsNullOrEmpty(original?.ToString()) ? original!.ToString()! : message.Id.Value;
        // A repeated scheduling call for the same delivery must not bypass broker duplicate detection.
        using var hash = SHA256.Create();
        var identity = JsonSerializer.Serialize(new[]
        {
            message.Id.Value, destination.Value,
            message.Header.HandledCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
        var retryId = "brighter-retry-" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "");
        var cloudEventId =
            message.Header.Bag.TryGetValue(ASBConstants.CloudEventsId, out var eventId)
            && !string.IsNullOrEmpty(eventId?.ToString()) ? eventId!.ToString()! : originalId;
        WriteRetryTelemetry(outgoing, destination, retryId, cloudEventId);
        var retry = AzureServiceBusMessagePublisher.ConvertToServiceBusMessage(outgoing);
        retry.Body = new BinaryData(message.Body.Memory);
        retry.MessageId = retryId;
        retry.ApplicationProperties[Message.OriginalMessageIdHeaderName] = originalId;
        retry.ApplicationProperties[ASBConstants.CloudEventsId] = cloudEventId;

        IServiceBusSenderWrapper? retrySender = null;
        try
        {
            retrySender = _senderProvider.Get(destination.Value);
            await retrySender.ScheduleMessageAsync(retry, _timeProvider.GetUtcNow().Add(delay), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error)
        {
            Log.SchedulingFailed(s_logger, error, destination.Value);
            throw new ChannelFailureException("Error scheduling a retry on the broker, see inner exception for details", error);
        }
        finally
        {
            if (retrySender is not null)
            {
                try
                {
                    await retrySender.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    // Closing a sender must not hide a scheduling failure or undo an accepted retry.
                    Log.ClosingSenderFailed(s_logger, error, destination.Value);
                }
            }
        }
    }

    private void WriteRetryTelemetry(Message outgoing, ChannelName destination, string retryId, string cloudEventId)
    {
        var activity = Activity.Current;
        if (activity is null) return;

        try
        {
            var tags = new ActivityTagsCollection();
            if (_instrumentationOptions != InstrumentationOptions.None)
                tags.Add(BrighterSemanticConventions.InstrumentationDomain, BrighterSemanticConventions.MessagingInstrumentationDomain);
            if (_instrumentationOptions.HasFlag(InstrumentationOptions.Messaging))
            {
                tags.Add(BrighterSemanticConventions.MessagingOperationType, CommandProcessorSpanOperation.Publish.ToSpanName());
                tags.Add(BrighterSemanticConventions.MessagingSystem, "azure_service_bus");
                tags.Add(BrighterSemanticConventions.MessagingDestination, destination.Value);
                tags.Add(BrighterSemanticConventions.MessagingDestinationPartitionId, outgoing.Header.PartitionKey.Value);
                tags.Add(BrighterSemanticConventions.MessageId, retryId);
                tags.Add(BrighterSemanticConventions.MessageHeaders,
                    JsonSerializer.Serialize(outgoing.Header, JsonSerialisationOptions.Options));
#pragma warning disable CS0618 // Preserve the legacy message type in telemetry.
                tags.Add(BrighterSemanticConventions.MessageType, outgoing.Header.MessageType.ToString());
#pragma warning restore CS0618
                tags.Add(BrighterSemanticConventions.MessageBodySize, outgoing.Body.Memory.Length);
                tags.Add(BrighterSemanticConventions.ConversationId, outgoing.Header.CorrelationId.Value);
            }
            if (_instrumentationOptions.HasFlag(InstrumentationOptions.RequestBody))
                tags.Add(BrighterSemanticConventions.MessageBody, outgoing.Body.Value);
            if (_instrumentationOptions.HasFlag(InstrumentationOptions.RequestInformation))
            {
                tags.Add(BrighterSemanticConventions.CeMessageId, cloudEventId);
                tags.Add(BrighterSemanticConventions.CeSource, outgoing.Header.Source);
                tags.Add(BrighterSemanticConventions.CeVersion, outgoing.Header.SpecVersion);
                tags.Add(BrighterSemanticConventions.CeSubject, outgoing.Header.Subject);
                tags.Add(BrighterSemanticConventions.CeType, outgoing.Header.Type.Value);
            }
            activity.AddEvent(new ActivityEvent($"{destination.Value} {CommandProcessorSpanOperation.Publish.ToSpanName()}",
                DateTimeOffset.UtcNow, tags));
        }
        catch (Exception error)
        {
            Log.TelemetryFailed(s_logger, error);
        }
        PropagateRetryContext(activity, outgoing);
    }

    private static void PropagateRetryContext(Activity activity, Message outgoing)
    {
        try
        {
            Propagators.DefaultTextMapPropagator.Inject(
                new PropagationContext(activity.Context, OpenTelemetry.Baggage.Current), outgoing, SetRetryContextHeader);
        }
        catch (Exception error)
        {
            Log.TracePropagationFailed(s_logger, error);
        }
    }

    private static void SetRetryContextHeader(Message outgoing, string key, string? value)
    {
        try
        {
            Message.PropogateContext(outgoing, key, value);
        }
        catch (Exception error)
        {
            // Rejecting optional baggage must not prevent the remaining trace headers from propagating.
            Log.TraceHeaderFailed(s_logger, error, key);
        }
    }

    private static Message CopyForRetry(Message message)
    {
        var header = message.Header;
#pragma warning disable CS0618 // Preserve the legacy message type for transport compatibility.
        var copy = new MessageHeader(header.MessageId, header.Topic, header.MessageType,
#pragma warning restore CS0618
            source: header.Source, type: header.Type, timeStamp: header.TimeStamp,
            correlationId: header.CorrelationId, replyTo: header.ReplyTo, contentType: header.ContentType,
            partitionKey: header.PartitionKey, dataSchema: header.DataSchema, subject: header.Subject,
            handledCount: header.HandledCount, workflowId: header.WorkflowId, jobId: header.JobId,
            delayed: header.Delayed, traceParent: header.TraceParent, traceState: header.TraceState)
        {
            DataRef = header.DataRef,
            SpecVersion = header.SpecVersion
        };
        foreach (var item in header.BagWithoutLocalHeaders())
            copy.Bag.Add(item.Key, item.Value);
        foreach (var item in header.Baggage)
            copy.Baggage.Add(item.Key, item.Value!);
        return new Message(copy, message.Body);
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Warning, "Failed to record retry telemetry; continuing with trace propagation")]
        public static partial void TelemetryFailed(ILogger logger, Exception error);

        [LoggerMessage(LogLevel.Warning, "Failed to propagate retry trace context; continuing with existing metadata")]
        public static partial void TracePropagationFailed(ILogger logger, Exception error);

        [LoggerMessage(LogLevel.Warning, "Failed to propagate retry trace header {Header}; continuing with existing metadata")]
        public static partial void TraceHeaderFailed(ILogger logger, Exception error, string header);

        [LoggerMessage(LogLevel.Error, "Failed to schedule retry on queue {Queue}")]
        public static partial void SchedulingFailed(ILogger logger, Exception error, string queue);

        [LoggerMessage(LogLevel.Warning, "Failed to close retry sender for queue {Queue}")]
        public static partial void ClosingSenderFailed(ILogger logger, Exception error, string queue);
    }

    /// <inheritdoc />
    public void Requeue(Message message, ChannelName destination, TimeSpan delay)
        => BrighterAsyncContext.Run(() => RequeueAsync(message, destination, delay));
}
