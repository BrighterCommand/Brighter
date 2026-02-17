using Google.Cloud.PubSub.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;


/// <summary>
/// A Brighter message consumer implementation for Google Cloud Pub/Sub using the **Pull** message delivery model.
/// This consumer polls the Pub/Sub service for messages.
/// </summary>
public partial class GcpPullMessageConsumer(
    GcpMessagingGatewayConnection connection,
    Google.Cloud.PubSub.V1.SubscriptionName subscriptionName,
    int batchSize,
    TimeProvider timeProvider)
    : IAmAMessageConsumerAsync, IAmAMessageConsumerSync
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<GcpPullMessageConsumer>();
    
    /// <summary>
    /// Synchronously acknowledges a message.
    /// </summary>
    /// <param name="message">The message to acknowledge.</param>
    public void Acknowledge(Message message)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var handler) || handler is not string ackId)
        {
            return;
        }

        try
        {
            var client = connection.GetOrCreateSubscriberServiceApiClient();
            client.Acknowledge(subscriptionName, [ackId]);
            Log.AcknowledgeSuccess(s_logger, message.Id, ackId, subscriptionName.ToString());
        }
        catch (Exception ex)
        {
            Log.AcknowledgeError(s_logger, ex, message.Id, ackId, subscriptionName.ToString());
            throw;
        }
    }

    /// <summary>
    /// Asynchronously acknowledges a message, deleting it from the subscription.
    /// </summary>
    /// <param name="message">The message to acknowledge.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var handler) || handler is not string ackId)
        {
            return;
        }

        try
        {
            var client = await connection.CreateSubscriberServiceApiClientAsync();
            await client.AcknowledgeAsync(subscriptionName, [ackId], cancellationToken);
            Log.AcknowledgeSuccess(s_logger, message.Id, ackId, subscriptionName.ToString());
        }
        catch (Exception ex)
        {
            Log.AcknowledgeError(s_logger, ex, message.Id, ackId, subscriptionName.ToString());
            throw;
        }
    }

    /// <summary>
    /// Nacks the specified message. For GCP Pub/Sub pull subscriptions, this is a no-op because not
    /// acknowledging the message is sufficient to allow redelivery after the ack deadline.
    /// </summary>
    /// <param name="message">The message.</param>
    public void Nack(Message message)
    {
        // No-op for GCP Pub/Sub: not acknowledging is sufficient for redelivery
    }

    /// <summary>
    /// Nacks the specified message. For GCP Pub/Sub pull subscriptions, this is a no-op because not
    /// acknowledging the message is sufficient to allow redelivery after the ack deadline.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancel the nack operation</param>
    public Task NackAsync(Message message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Purge()
    {
        try
        {
            var client = connection.GetOrCreateSubscriberServiceApiClient();

            Log.PurgeStart(s_logger, subscriptionName.ToString());
            client.Seek(
                new SeekRequest { Time = Timestamp.FromDateTimeOffset(timeProvider.GetUtcNow().AddMinutes(1)) });
            Log.PurgeComplete(s_logger, subscriptionName.ToString());
        }
        catch (Exception ex)
        {
            Log.PurgeError(s_logger, ex, subscriptionName.ToString());
            throw;
        }
    }

    /// <summary>
    /// Asynchronously purges all unacknowledged messages from the subscription.
    /// This is done by seeking the subscription to a point in time one minute in the future.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await connection.CreateSubscriberServiceApiClientAsync();

            Log.PurgeStart(s_logger, subscriptionName.ToString());

            await client.SeekAsync(
                new SeekRequest { Time = Timestamp.FromDateTimeOffset(timeProvider.GetUtcNow().AddMinutes(1)) },
                cancellationToken);

            Log.PurgeComplete(s_logger, subscriptionName.ToString());
        }
        catch (Exception ex)
        {
            Log.PurgeError(s_logger, ex, subscriptionName.ToString());
            throw;
        }
    }

    /// <summary>
    /// Asynchronously receives a batch of messages from the subscription using the Pull API.
    /// </summary>
    /// <param name="timeOut">A timeout value (not strictly used by the underlying Google Pub/Sub client, but part of the Brighter interface).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that returns an array of received Brighter messages. Returns an array containing a single empty message if no messages are available.</returns>
    public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
    {
        PullResponse response;
        try
        {
            var client = await connection.CreateSubscriberServiceApiClientAsync();
            response = await client.PullAsync(
                new PullRequest
                {
                    SubscriptionAsSubscriptionName = subscriptionName, 
                    MaxMessages = batchSize,
                },
                cancellationToken);

            if (response.ReceivedMessages.Count == 0)
            {
                return [new Message()];
            }
        }
        catch (RpcException rcpException) when (rcpException.Status.StatusCode == StatusCode.Unavailable)
        {
            Log.ReceiveConnectionError(s_logger);
            throw new ChannelFailureException("Error connecting to Pub/Sub, see inner exception for details",
                rcpException);
        }
        catch (Exception e)
        {
            Log.ReceiveError(s_logger, e, subscriptionName.ToString());
            throw;
        }

        return response.ReceivedMessages.Select(Parser.ToBrighterMessage).ToArray();
    }

    
    /// <summary>
    /// Synchronously receives a batch of messages from the subscription using the Pull API.
    /// </summary>
    /// <param name="timeOut">A timeout value (not strictly used by the underlying Google Pub/Sub client).</param>
    /// <returns>An array of received Brighter messages. Returns an array containing a single empty message if no messages are available.</returns>

    public Message[] Receive(TimeSpan? timeOut = null)
    {
        PullResponse response;
        try
        {
            var client = connection.GetOrCreateSubscriberServiceApiClient();
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
            Log.ReceiveConnectionError(s_logger);
            throw new ChannelFailureException("Error connecting to Pub/Sub, see inner exception for details",
                rcpException);
        }
        catch (Exception e)
        {
            Log.ReceiveError(s_logger, e, subscriptionName.ToString());
            throw;
        }

        return response.ReceivedMessages.Select(Parser.ToBrighterMessage).ToArray();
    }
    
       /// <summary>
    /// Synchronously rejects a message.
    /// </summary>
    /// <param name="message">The message to reject.</param>
    /// <param name="reason">The <see cref="MessageRejectionReason"/> that explains why we rejected the message</param>
    /// <returns>True if the message was successfully rejected/acknowledged, otherwise false.</returns>
    public bool Reject(Message message, MessageRejectionReason? reason = null)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var handler) || handler is not string ackId)
        {
            return false;
        }

        try
        {
            var client = connection.GetOrCreateSubscriberServiceApiClient();

            Log.RejectMessage(s_logger, message.Id, ackId, subscriptionName.ToString());
            client.Acknowledge(subscriptionName, [ackId]);
        }
        catch (Exception ex)
        {
            Log.RejectError(s_logger, ex, message.Id, ackId, subscriptionName.ToString());
            throw;
        }

        return true;
    }
    
    /// <summary>
    /// Asynchronously rejects a message.
    /// </summary>
    /// <param name="message">The message to reject.</param>
    /// <param name="reason">The <see cref="MessageRejectionReason"/> that explains why we rejected the message</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that returns true if the message was successfully rejected/acknowledged, otherwise false.</returns>
    public async Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var handler) || handler is not string ackId)
        {
            return false;
        }

        try
        {
            var client = await connection.CreateSubscriberServiceApiClientAsync();
            Log.RejectMessage(s_logger, message.Id, ackId, subscriptionName.ToString());
            await client.AcknowledgeAsync(subscriptionName, [ackId], cancellationToken);
        }
        catch (Exception ex)
        {
            Log.RejectError(s_logger, ex, message.Id, ackId, subscriptionName.ToString());
            throw;
        }

        return true;
    }


    /// <summary>
    /// Synchronously requeues a message by setting its acknowledgment deadline to zero seconds.
    /// </summary>
    /// <param name="message">The message to requeue.</param>
    /// <param name="delay">An optional delay (not used by Pub/Sub).</param>
    /// <returns>True if the message was successfully requeued, otherwise false.</returns>
    public bool Requeue(Message message, TimeSpan? delay = null)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var handler) || handler is not string ackId)
        {
            return false;
        }

        try
        {
            var client = connection.GetOrCreateSubscriberServiceApiClient();

            Log.RequeueStart(s_logger, message.Id);

            // The requeue policy is defined by subscription, during its creation
            client.ModifyAckDeadline(subscriptionName, [ackId], 0);

            Log.RequeueComplete(s_logger, message.Id);
            return true;
        }
        catch (Exception ex)
        {
            Log.RequeueError(s_logger, ex, message.Id, ackId, subscriptionName.ToString());
            return false;
        }
    }
    
    /// <summary>
    /// Asynchronously requeues a message by setting its acknowledgment deadline to zero seconds.
    /// This tells Pub/Sub to immediately redeliver the message according to the subscription's retry policy.
    /// </summary>
    /// <param name="message">The message to requeue.</param>
    /// <param name="delay">An optional delay (not used by Pub/Sub, as requeue delay is set by the subscription's RetryPolicy).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that returns true if the message was successfully requeued, otherwise false.</returns>
    public async Task<bool> RequeueAsync(Message message, TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var handler) || handler is not string ackId)
        {
            return false;
        }

        try
        {
            var client = await connection.CreateSubscriberServiceApiClientAsync();

            Log.RequeueStart(s_logger, message.Id);

            // The requeue policy is defined by subscription, during its creation
            await client.ModifyAckDeadlineAsync(new ModifyAckDeadlineRequest
            {
                SubscriptionAsSubscriptionName = subscriptionName,
                AckIds = { ackId },
                AckDeadlineSeconds = 0
            }, cancellationToken);

            Log.RequeueComplete(s_logger, message.Id);
            return true;
        }
        catch (Exception ex)
        {
            Log.RequeueError(s_logger, ex, message.Id, ackId, subscriptionName.ToString());
            return false;
        }
    }

    public ValueTask DisposeAsync()
    {
        return new ValueTask();
    }

    public void Dispose()
    {
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Information,
            "GcpPullMessageConsumer: The message {Id} acknowledged with the receipt handle {ReceiptHandle} on the subscription {SubscriptionName}")]
        public static partial void AcknowledgeSuccess(ILogger logger, string id, string receiptHandle,
            string subscriptionName);

        [LoggerMessage(LogLevel.Error,
            "GcpPullMessageConsumer: Error during acknowledging the message {Id} with the receipt handle {ReceiptHandle} on the subscription {SubscriptionName}")]
        public static partial void AcknowledgeError(ILogger logger, Exception ex, string id, string receiptHandle,
            string subscriptionName);

        [LoggerMessage(LogLevel.Information,
            "PullPubSubConsumer: Rejecting the message {Id} with the receipt handle {ReceiptHandle} on the subscription {SubscriptionName}")]
        public static partial void RejectMessage(ILogger logger, string id, string receiptHandle,
            string subscriptionName);

        [LoggerMessage(LogLevel.Error,
            "PullPubSubConsumer: Error during rejecting the message {Id} with the receipt handle {ReceiptHandle} on the subscription {SubscriptionName}")]
        public static partial void RejectError(ILogger logger, Exception ex, string id, string receiptHandle,
            string subscriptionName);

        [LoggerMessage(LogLevel.Information, "PullPubSubConsumer: Purging the subscription {SubscriptionName}")]
        public static partial void PurgeStart(ILogger logger, string subscriptionName);

        [LoggerMessage(LogLevel.Information, "PullPubSubConsumer: Purged the subscription {SubscriptionName}")]
        public static partial void PurgeComplete(ILogger logger, string subscriptionName);

        [LoggerMessage(LogLevel.Error, "PullPubSubConsumer: Error during purging the subscription {SubscriptionName}")]
        public static partial void PurgeError(ILogger logger, Exception ex, string subscriptionName);

        [LoggerMessage(LogLevel.Debug, "PullPubSubConsumer: Could not determine number of messages to retrieve")]
        public static partial void ReceiveConnectionError(ILogger logger);

        [LoggerMessage(LogLevel.Error, "PullPubSubConsumer: There was an error listening to queue {SubscriptionName}")]
        public static partial void ReceiveError(ILogger logger, Exception ex, string subscriptionName);

        [LoggerMessage(LogLevel.Information, "PullPubSubConsumer: re-queueing the message {Id}")]
        public static partial void RequeueStart(ILogger logger, string id);

        [LoggerMessage(LogLevel.Information, "PullPubSubConsumer: re-queued the message {Id}")]
        public static partial void RequeueComplete(ILogger logger, string id);

        [LoggerMessage(LogLevel.Error,
            "PullPubSubConsumer: Error during re-queueing the message {Id} with receipt handle {ReceiptHandle} on the subscription {SubscriptionName}")]
        public static partial void RequeueError(ILogger logger, Exception ex, string id, string receiptHandle,
            string subscriptionName);
    }
}
