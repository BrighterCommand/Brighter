using Google.Cloud.PubSub.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The Pull based implementation of Pub/Sub
/// </summary>
/// <param name="client">The subscriber api.</param>
/// <param name="subscriptionName">The subscription name</param>
/// <param name="batchSize">The pull batch size</param>
/// <param name="hasDql">flag indicating it it has a dead letter queue</param>
/// <param name="timeProvider">The <see cref="System.TimeProvider"/></param>
public class PullPubSubConsumer(
    SubscriberServiceApiClient client,
    Google.Cloud.PubSub.V1.SubscriptionName subscriptionName,
    int batchSize,
    bool hasDql,
    TimeProvider timeProvider) : IAmAMessageConsumerAsync, IAmAMessageConsumerSync
{
    private static readonly ILogger Logger = ApplicationLogging.CreateLogger<PullPubSubConsumer>();


    /// <inheritdoc />
    public async Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var handler) || handler is not string ackId)
        {
            return;
        }

        try
        {
            await client.AcknowledgeAsync(subscriptionName, [ackId], cancellationToken);
            Logger.LogInformation(
                "PullPubSubConsumer: The message {Id} acknowledged with the receipt handle {ReceiptHandle} on the subscription {ChannelName}",
                message.Id, ackId, subscriptionName.ToString());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "PullPubSubConsumer: Error during acknowledging the message {Id} with the receipt handle {ReceiptHandle} on the subscription {ChannelName}",
                message.Id, ackId, subscriptionName.ToString());
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RejectAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var handler) || handler is not string ackId)
        {
            return;
        }

        try
        {
            Logger.LogInformation(
                "PullPubSubConsumer: Rejecting the message {Id} with the receipt handle {ReceiptHandle} on the subscription {ChannelName}",
                message.Id, ackId, subscriptionName.ToString());

            if (hasDql)
            {
                await client.ModifyAckDeadlineAsync(subscriptionName, [ackId], 0, cancellationToken);
            }
            else
            {
                await client.AcknowledgeAsync(subscriptionName, [ackId], cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "PullPubSubConsumer: Error during rejecting the message {Id} with the receipt handle {ReceiptHandle} on the subscription {ChannelName}",
                message.Id, ackId, subscriptionName.ToString());
            throw;
        }
    }

    /// <inheritdoc />
    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("PullPubSubConsumer: Purging the subscription {ChannelName}",
                subscriptionName.ToString());

            await client.SeekAsync(
                new SeekRequest { Time = Timestamp.FromDateTimeOffset(timeProvider.GetUtcNow().AddMinutes(1)), },
                cancellationToken);

            Logger.LogInformation("PullPubSubConsumer: Purged the subscription {ChannelName}",
                subscriptionName.ToString());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "PullPubSubConsumer: Error during purging the subscription {ChannelName}",
                subscriptionName.ToString());
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null,
        CancellationToken cancellationToken = default)
    {
        PullResponse response;
        try
        {
            response = await client.PullAsync(
                new PullRequest { SubscriptionAsSubscriptionName = subscriptionName, MaxMessages = batchSize },
                cancellationToken);

            if (response.ReceivedMessages.Count == 0)
            {
                return [new Message()];
            }
        }
        catch (RpcException rcpException) when (rcpException.Status.StatusCode == StatusCode.Unavailable)
        {
            Logger.LogDebug("PullPubSubConsumer: Could not determine number of messages to retrieve");
            throw new ChannelFailureException("Error connection to Pub/Sub, see inner exception for details",
                rcpException);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "PullPubSubConsumer: There was an error listening to queue {ChannelName} ",
                subscriptionName.ToString());
            throw;
        }

        return response.ReceivedMessages
            .Select(Parser.ToBrighterMessage)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<bool> RequeueAsync(Message message, TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var handler) || handler is not string ackId)
        {
            return false;
        }

        try
        {
            Logger.LogInformation("PullPubSubConsumer: re-queueing the message {Id}", message.Id);

            // The requeue policy is defined by subscription, during it creating
            await client.ModifyAckDeadlineAsync(subscriptionName, [ackId], 0, cancellationToken);

            Logger.LogInformation("PullPubSubConsumer: re-queued the message {Id}", message.Id);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "PullPubSubConsumer: Error during re-queueing the message {Id} with receipt handle {ReceiptHandle} on the queue {ChannelName}",
                message.Id, ackId, subscriptionName.ToString());
            return false;
        }
    }

    /// <inheritdoc />
    public void Acknowledge(Message message)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var handler) || handler is not string ackId)
        {
            return;
        }

        try
        {
            client.Acknowledge(subscriptionName, [ackId]);
            Logger.LogInformation(
                "PullPubSubConsumer: The message {Id} acknowledged with the receipt handle {ReceiptHandle} on the subscription {ChannelName}",
                message.Id, ackId, subscriptionName.ToString());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "PullPubSubConsumer: Error during acknowledging the message {Id} with the receipt handle {ReceiptHandle} on the subscription {ChannelName}",
                message.Id, ackId, subscriptionName.ToString());
            throw;
        }
    }

    /// <inheritdoc />
    public void Reject(Message message)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var handler) || handler is not string ackId)
        {
            return;
        }

        try
        {
            Logger.LogInformation(
                "PullPubSubConsumer: Rejecting the message {Id} with the receipt handle {ReceiptHandle} on the subscription {ChannelName}",
                message.Id, ackId, subscriptionName.ToString());

            if (hasDql)
            {
                client.ModifyAckDeadline(subscriptionName, [ackId], 0);
            }
            else
            {
                client.Acknowledge(subscriptionName, [ackId]);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "PullPubSubConsumer: Error during rejecting the message {Id} with the receipt handle {ReceiptHandle} on the subscription {ChannelName}",
                message.Id, ackId, subscriptionName.ToString());
            throw;
        }
    }

    /// <inheritdoc />
    public void Purge()
    {
        try
        {
            Logger.LogInformation("PullPubSubConsumer: Purging the subscription {ChannelName}",
                subscriptionName.ToString());

            client.Seek(
                new SeekRequest
                {
                    Time = Timestamp.FromDateTimeOffset(timeProvider.GetUtcNow().AddMinutes(1)
                    )
                });

            Logger.LogInformation("PullPubSubConsumer: Purged the subscription {ChannelName}",
                subscriptionName.ToString());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "PullPubSubConsumer: Error during purging the subscription {ChannelName}",
                subscriptionName.ToString());
            throw;
        }
    }

    /// <inheritdoc />
    public Message[] Receive(TimeSpan? timeOut = null)
    {
        PullResponse response;
        try
        {
            response = client.Pull(new PullRequest
            {
                SubscriptionAsSubscriptionName = subscriptionName, MaxMessages = batchSize
            });

            if (response.ReceivedMessages.Count == 0)
            {
                return [new Message()];
            }
        }
        catch (RpcException rcpException) when (rcpException.Status.StatusCode == StatusCode.Unavailable)
        {
            Logger.LogDebug("PullPubSubConsumer: Could not determine number of messages to retrieve");
            throw new ChannelFailureException("Error connection to Pub/Sub, see inner exception for details",
                rcpException);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "PullPubSubConsumer: There was an error listening to queue {ChannelName} ",
                subscriptionName.ToString());
            throw;
        }

        return response.ReceivedMessages
            .Select(Parser.ToBrighterMessage)
            .ToArray();
    }

    /// <inheritdoc />
    public bool Requeue(Message message, TimeSpan? delay = null)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var handler) || handler is not string ackId)
        {
            return false;
        }

        try
        {
            Logger.LogInformation("PullPubSubConsumer: re-queueing the message {Id}", message.Id);

            // The requeue policy is defined by subscription, during it creating
            client.ModifyAckDeadlineAsync(subscriptionName, [ackId], 0);

            Logger.LogInformation("PullPubSubConsumer: re-queued the message {Id}", message.Id);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "PullPubSubConsumer: Error during re-queueing the message {Id} with receipt handle {ReceiptHandle} on the queue {ChannelName}",
                message.Id, ackId, subscriptionName.ToString());
            return false;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return new ValueTask();
    }


    /// <inheritdoc />
    public void Dispose()
    {
    }
}
