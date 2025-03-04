using Google.Cloud.PubSub.V1;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

public class PullPubSubConsumer(
    SubscriberServiceApiClient client,
    Google.Cloud.PubSub.V1.SubscriptionName subscriptionName,
    int batchSize,
    bool hasDql,
    TimeProvider timeProvider) : IAmAMessageConsumerAsync
{
    private static readonly ILogger Logger = ApplicationLogging.CreateLogger<PullPubSubConsumer>();

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return new ValueTask();
    }

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
        var response =
            await client.PullAsync(
                new PullRequest { SubscriptionAsSubscriptionName = subscriptionName, MaxMessages = batchSize },
                cancellationToken);

        if (response.ReceivedMessages.Count == 0)
        {
            return [new Message()];
        }
        
        
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
}
