using Google.Cloud.PubSub.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The pull-based implementation of Pub/Sub
/// </summary>
public partial class GcpPullMessageConsumer : IAmAMessageConsumerAsync, IAmAMessageConsumerSync
{
    private readonly GcpMessagingGatewayConnection _connection;
    private readonly Google.Cloud.PubSub.V1.SubscriptionName _subscriptionName;
    private readonly int _batchSize;
    private readonly bool _hasDql;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The Pull based implementation of Pub/Sub
    /// </summary>
    /// <param name="connection">The <see cref="GcpMessagingGatewayConnection"/>.</param>
    /// <param name="subscriptionName">The subscription name</param>
    /// <param name="batchSize">The pull batch size</param>
    /// <param name="hasDql">flag indicating it has a dead letter queue</param>
    /// <param name="timeProvider">The <see cref="System.TimeProvider"/></param>
    public GcpPullMessageConsumer(
        GcpMessagingGatewayConnection connection,
        Google.Cloud.PubSub.V1.SubscriptionName subscriptionName,
        int batchSize = 1,
        bool hasDql = false,
        TimeProvider? timeProvider = null)
    {
        _connection = connection;
        _subscriptionName = subscriptionName;
        _batchSize = batchSize;
        _hasDql = hasDql;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<GcpPullMessageConsumer>();

    /// <inheritdoc />
    public async Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var handler) || handler is not string ackId)
        {
            return;
        }

        try
        {
            var client = await _connection.CreateSubscriberServiceApiClientAsync();
            await client.AcknowledgeAsync(_subscriptionName, [ackId], cancellationToken);
            Log.AcknowledgeSuccess(s_logger, message.Id, ackId, _subscriptionName.ToString());
        }
        catch (Exception ex)
        {
            Log.AcknowledgeError(s_logger, ex, message.Id, ackId, _subscriptionName.ToString());
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RejectAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var handler) || handler is not string ackId)
        {
            return false;
        }

        try
        {
            var client = await _connection.CreateSubscriberServiceApiClientAsync();
            Log.RejectMessage(s_logger, message.Id, ackId, _subscriptionName.ToString());
            if (_hasDql)
            {
                await client.ModifyAckDeadlineAsync(_subscriptionName, [ackId], 0, cancellationToken);
            }
            else
            {
                await client.AcknowledgeAsync(_subscriptionName, [ackId], cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Log.RejectError(s_logger, ex, message.Id, ackId, _subscriptionName.ToString());
            throw;
        }

        return true;
    }

    /// <inheritdoc />
    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await _connection.CreateSubscriberServiceApiClientAsync();

            Log.PurgeStart(s_logger, _subscriptionName.ToString());

            await client.SeekAsync(
                new SeekRequest { Time = Timestamp.FromDateTimeOffset(_timeProvider.GetUtcNow().AddMinutes(1)) },
                cancellationToken);

            Log.PurgeComplete(s_logger, _subscriptionName.ToString());
        }
        catch (Exception ex)
        {
            Log.PurgeError(s_logger, ex, _subscriptionName.ToString());
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
    {
        PullResponse response;
        try
        {
            var client = await _connection.CreateSubscriberServiceApiClientAsync();
            response = await client.PullAsync(
                new PullRequest
                {
                    SubscriptionAsSubscriptionName = _subscriptionName, 
                    MaxMessages = _batchSize,
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
            Log.ReceiveError(s_logger, e, _subscriptionName.ToString());
            throw;
        }

        return response.ReceivedMessages.Select(Parser.ToBrighterMessage).ToArray();
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
            var client = await _connection.CreateSubscriberServiceApiClientAsync();

            Log.RequeueStart(s_logger, message.Id);

            // The requeue policy is defined by subscription, during its creation
            await client.ModifyAckDeadlineAsync(new ModifyAckDeadlineRequest
            {
                SubscriptionAsSubscriptionName = _subscriptionName,
                AckIds = { ackId },
                AckDeadlineSeconds = 0
            }, cancellationToken);

            Log.RequeueComplete(s_logger, message.Id);
            return true;
        }
        catch (Exception ex)
        {
            Log.RequeueError(s_logger, ex, message.Id, ackId, _subscriptionName.ToString());
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
            var client = _connection.CreateSubscriberServiceApiClient();
            client.Acknowledge(_subscriptionName, [ackId]);
            Log.AcknowledgeSuccess(s_logger, message.Id, ackId, _subscriptionName.ToString());
        }
        catch (Exception ex)
        {
            Log.AcknowledgeError(s_logger, ex, message.Id, ackId, _subscriptionName.ToString());
            throw;
        }
    }

    /// <inheritdoc />
    public bool Reject(Message message)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var handler) || handler is not string ackId)
        {
            return false;
        }

        try
        {
            var client = _connection.CreateSubscriberServiceApiClient();

            Log.RejectMessage(s_logger, message.Id, ackId, _subscriptionName.ToString());
            if (_hasDql)
            {
                client.ModifyAckDeadline(_subscriptionName, [ackId], 0);
            }
            else
            {
                client.Acknowledge(_subscriptionName, [ackId]);
            }
        }
        catch (Exception ex)
        {
            Log.RejectError(s_logger, ex, message.Id, ackId, _subscriptionName.ToString());
            throw;
        }

        return true;
    }

    /// <inheritdoc />
    public void Purge()
    {
        try
        {
            var client = _connection.CreateSubscriberServiceApiClient();

            Log.PurgeStart(s_logger, _subscriptionName.ToString());
            client.Seek(
                new SeekRequest { Time = Timestamp.FromDateTimeOffset(_timeProvider.GetUtcNow().AddMinutes(1)) });
            Log.PurgeComplete(s_logger, _subscriptionName.ToString());
        }
        catch (Exception ex)
        {
            Log.PurgeError(s_logger, ex, _subscriptionName.ToString());
            throw;
        }
    }

    /// <inheritdoc />
    public Message[] Receive(TimeSpan? timeOut = null)
    {
        PullResponse response;
        try
        {
            var client = _connection.CreateSubscriberServiceApiClient();
            response = client.Pull(new PullRequest
            {
                SubscriptionAsSubscriptionName = _subscriptionName, MaxMessages = _batchSize
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
            Log.ReceiveError(s_logger, e, _subscriptionName.ToString());
            throw;
        }

        return response.ReceivedMessages.Select(Parser.ToBrighterMessage).ToArray();
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
            var client = _connection.CreateSubscriberServiceApiClient();

            Log.RequeueStart(s_logger, message.Id);

            // The requeue policy is defined by subscription, during its creation
            client.ModifyAckDeadline(_subscriptionName, [ackId], 0);

            Log.RequeueComplete(s_logger, message.Id);
            return true;
        }
        catch (Exception ex)
        {
            Log.RequeueError(s_logger, ex, message.Id, ackId, _subscriptionName.ToString());
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

    /// <summary>
    /// Internal logging class
    /// </summary>
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
