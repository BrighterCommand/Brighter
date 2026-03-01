using Google.Cloud.PubSub.V1;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// Implements a synchronous and asynchronous message consumer for Google Cloud Pub/Sub,
/// integrating a streaming consumer approach with message bus abstractions.
/// </summary>
/// <remarks>
/// This consumer uses the <see cref="GcpMessagingGatewayConnection"/> to access the
/// low-level client for management operations like Purge, and uses an internal
/// channel reader for message reception.
/// </remarks>
public partial class GcpPubSubStreamMessageConsumer(
    GcpMessagingGatewayConnection connection,
    GcpStreamConsumer consumer,
    Google.Cloud.PubSub.V1.SubscriptionName subscriptionName,
    TimeProvider timeProvider) : IAmAMessageConsumerSync, IAmAMessageConsumerAsync
{

    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<GcpPubSubStreamMessageConsumer>();
    
    /// <summary>
    /// Synchronously acknowledges a message, signalling the Pub/Sub service that the message
    /// has been successfully processed and can be discarded.
    /// </summary>
    /// <param name="message">The message to acknowledge, containing the receipt handle in its header bag.</param>
    public void Acknowledge(Message message)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var receiptHandle) || receiptHandle is not GcpStreamMessage gcpStreamMessage)
        {
            return;
        }
        
        gcpStreamMessage.Accepted();
        Log.AcknowledgeSuccess(s_logger, message.Id, "", subscriptionName.ToString());
    }
    
    /// <summary>
    /// Asynchronously acknowledges a message, signalling the Pub/Sub service that the message
    /// has been successfully processed and can be discarded.
    /// </summary>
    /// <param name="message">The message to acknowledge, containing the receipt handle in its header bag.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completed task.</returns>
    public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
    {
        Acknowledge(message);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Nacks the specified message. For GCP Pub/Sub (stream-based), this is a no-op because not
    /// acknowledging the message is sufficient to allow redelivery.
    /// </summary>
    /// <param name="message">The message.</param>
    public void Nack(Message message)
    {
        // No-op for GCP Pub/Sub: not acknowledging is sufficient for redelivery
    }

    /// <summary>
    /// Nacks the specified message. For GCP Pub/Sub (stream-based), this is a no-op because not
    /// acknowledging the message is sufficient to allow redelivery.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancel the nack operation</param>
    public Task NackAsync(Message message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Synchronously rejects a message. In this implementation, it calls <see cref="GcpStreamMessage.Accepted"/>
    /// to signal processing completion and prevents redelivery, while logging the rejection.
    /// </summary>
    /// <param name="message">The message to reject.</param>
    /// <param name="reason">The <see cref="MessageRejectionReason"/> that explains why we rejected the message</param>
    /// <returns>Always returns <c>true</c> indicating the operation was processed.</returns>
    public bool Reject(Message message, MessageRejectionReason? reason = null)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var receiptHandle) || receiptHandle is not GcpStreamMessage gcpStreamMessage)
        {
            return true;
        }
        
        gcpStreamMessage.Accepted();
        Log.RejectMessage(s_logger, message.Id, "", subscriptionName.ToString());
        return true;
    }
    
    /// <summary>
    /// Asynchronously rejects a message. In this implementation, it calls <see cref="GcpStreamMessage.Accepted"/>
    /// to signal processing completion and prevents redelivery, while logging the rejection.
    /// </summary>
    /// <param name="message">The message to reject.</param>
    /// <param name="reason">The <see cref="MessageRejectionReason"/> that explains why we rejected the message</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that returns <c>true</c>.</returns>
    public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Reject(message, reason));
    }

    /// <summary>
    /// Synchronously purges all messages from the subscription backlog by executing a 
    /// Pub/Sub Seek operation to a timestamp slightly in the future.
    /// </summary>
    /// <exception cref="Exception">Throws if the underlying Seek operation fails.</exception>
    public void Purge()
    {
        try
        {
            var client = connection.GetOrCreateSubscriberServiceApiClient();

            Log.PurgeStart(s_logger, subscriptionName.ToString());

            client.Seek(new SeekRequest
            {
                Time = Timestamp.FromDateTimeOffset(timeProvider.GetUtcNow().AddMinutes(1))
            });

            Log.PurgeComplete(s_logger, subscriptionName.ToString());
        }
        catch (Exception ex)
        {
            Log.PurgeError(s_logger, ex, subscriptionName.ToString());
            throw;
        }
    }
    
    /// <summary>
    /// Asynchronously purges all messages from the subscription backlog by executing a 
    /// Pub/Sub Seek operation to a timestamp slightly in the future.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="Exception">Throws if the underlying Seek operation fails.</exception>
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
    /// Synchronously reads the next message from the internal channel reader, blocking 
    /// until a message is available or the timeout is reached.
    /// </summary>
    /// <param name="timeOut">Optional timeout for the receive operation.</param>
    /// <returns>An array containing one message if available, or an array with a default message if a timeout occurs.</returns>
    public Message[] Receive(TimeSpan? timeOut = null)
    {
        return BrighterAsyncContext.Run(() => ReceiveAsync(timeOut));
    }
    
    /// <summary>
    /// Asynchronously reads the next message from the internal channel reader, waiting 
    /// until a message is available or the timeout is reached.
    /// </summary>
    /// <param name="timeOut">Optional timeout for the receive operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task returning an array containing one message if available, or an array with a default message if a timeout occurs.</returns>
    public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeOut.HasValue)
        {
            cts.CancelAfter(timeOut.Value);
        }

        var reader = consumer.Reader;
        try
        {
            while (!cts.IsCancellationRequested)
            {
                var message = await reader.ReadAsync(cts.Token);
                if (message.CanProcess)
                {
                    return [Parser.ToBrighterMessage(message)];
                }
            }
            
            return [new Message()];
        }
        catch (OperationCanceledException)
        {
            return [new Message()];
        }
    }

    /// <summary>
    /// Synchronously requeues a message by calling <see cref="GcpStreamMessage.Reject"/> 
    /// on the receipt handle. This signals the service to redeliver the message.
    /// </summary>
    /// <param name="message">The message to requeue.</param>
    /// <param name="delay">Optional delay requested for redelivery (may not be strictly honored by Pub/Sub).</param>
    /// <returns>Always returns <c>true</c>.</returns>
    public bool Requeue(Message message, TimeSpan? delay = null)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var receiptHandle) || receiptHandle is not GcpStreamMessage gcpStreamMessage)
        {
            return true;
        }
        
        gcpStreamMessage.Reject();
        Log.RequeueComplete(s_logger, message.Id);
        return true;
    }

    /// <summary>
    /// Asynchronously requeues a message by calling <see cref="GcpStreamMessage.Reject"/> 
    /// on the receipt handle. This signals the service to redeliver the message.
    /// </summary>
    /// <param name="message">The message to requeue.</param>
    /// <param name="delay">Optional delay requested for redelivery (may not be strictly honored by Pub/Sub).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that returns <c>true</c>.</returns>
    public Task<bool> RequeueAsync(Message message, TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Requeue(message, delay));
    }
    
    /// <summary>
    /// Disposes of the consumer's resources synchronously.
    /// </summary>
    public void Dispose()
    {
        consumer.StopAsync().GetAwaiter().GetResult();
    }
    
    /// <summary>
    /// Disposes of the consumer's resources asynchronously.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await consumer.StopAsync();
    }
    
    private static partial class Log
    {
        [LoggerMessage(LogLevel.Information, "GcpStreamMessageConsumer: The message {Id} acknowledged with the receipt handle {ReceiptHandle} on the subscription {SubscriptionName}")]
        public static partial void AcknowledgeSuccess(ILogger logger, string id, string receiptHandle,
            string subscriptionName);

        [LoggerMessage(LogLevel.Information,
            "GcpStreamMessageConsumer: Rejecting the message {Id} with the receipt handle {ReceiptHandle} on the subscription {SubscriptionName}")]
        public static partial void RejectMessage(ILogger logger, string id, string receiptHandle,
            string subscriptionName);

        [LoggerMessage(LogLevel.Error,
            "PullPubSubConsumer: Error during rejecting the message {Id} with the receipt handle {ReceiptHandle} on the subscription {SubscriptionName}")]
        public static partial void RejectError(ILogger logger, Exception ex, string id, string receiptHandle,
            string subscriptionName);

        [LoggerMessage(LogLevel.Information, "GcpStreamMessageConsumer: Purging the subscription {SubscriptionName}")]
        public static partial void PurgeStart(ILogger logger, string subscriptionName);

        [LoggerMessage(LogLevel.Information, "GcpStreamMessageConsumer: Purged the subscription {SubscriptionName}")]
        public static partial void PurgeComplete(ILogger logger, string subscriptionName);

        [LoggerMessage(LogLevel.Error, "GcpStreamMessageConsumer: Error during purging the subscription {SubscriptionName}")]
        public static partial void PurgeError(ILogger logger, Exception ex, string subscriptionName);

        [LoggerMessage(LogLevel.Information, "PullPubSubConsumer: re-queued the message {Id}")]
        public static partial void RequeueComplete(ILogger logger, string id);
    }
}
