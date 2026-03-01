using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Org.Apache.Rocketmq;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.RocketMQ;

/// <summary>
/// RocketMQ message consumer implementation for Brighter.
/// Integrates RocketMQ's consumer group pattern and message filtering capabilities.
/// </summary>
/// <param name="consumer">The underlying RocketMQ simple consumer.</param>
/// <param name="bufferSize">The number of messages to retrieve per receive call.</param>
/// <param name="invisibilityTimeout">How long messages remain invisible after being received.</param>
/// <param name="connection">The gateway connection configuration, used for lazy DLQ producer creation.</param>
/// <param name="deadLetterRoutingKey">The routing key for the dead letter queue topic.</param>
/// <param name="invalidMessageRoutingKey">The routing key for the invalid message topic.</param>
public partial class RocketMessageConsumer(SimpleConsumer consumer,
    int bufferSize,
    TimeSpan invisibilityTimeout,
    RocketMessagingGatewayConnection? connection = null,
    RoutingKey? deadLetterRoutingKey = null,
    RoutingKey? invalidMessageRoutingKey = null)
    : IAmAMessageConsumerAsync, IAmAMessageConsumerSync
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RocketMessageConsumer>();

    private readonly RocketMessagingGatewayConnection? _connection = connection;
    private readonly RoutingKey? _deadLetterRoutingKey = deadLetterRoutingKey;
    private readonly RoutingKey? _invalidMessageRoutingKey = invalidMessageRoutingKey;
    // Thread-safe: message pumps are single-threaded per consumer, so null-coalescing
    // assignment in GetProducerForRouteAsync() cannot race.
    private RocketMqMessageProducer? _deadLetterProducer;
    private RocketMqMessageProducer? _invalidMessageProducer;

    /// <inheritdoc />
    public void Acknowledge(Message message) 
        => BrighterAsyncContext.Run(() => AcknowledgeAsync(message));
    
    /// <inheritdoc />
    public async Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var handler) || handler is not MessageView view)
        {
           return;
        }
        
        await consumer.Ack(view);
    }
    
    /// <inheritdoc />
    public void Purge() 
        => BrighterAsyncContext.Run(() => PurgeAsync());

    /// <inheritdoc />
    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var messages = await consumer.Receive(bufferSize, invisibilityTimeout);
            if (messages == null || messages.Count == 0)
            {
                break;
            }
            
            await messages.EachAsync(async message => await consumer.Ack(message));
        }
    }
    
    /// <inheritdoc />
    public Message[] Receive(TimeSpan? timeOut = null)
        => BrighterAsyncContext.Run(() => ReceiveAsync(timeOut));

    /// <inheritdoc />
    public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        var messageView = await consumer.Receive(bufferSize, invisibilityTimeout);
        if (messageView == null || messageView.Count == 0)
        {
            return [new Message()];
        }
        
        var messages = new Message[messageView.Count];
        for (int i = 0; i < messageView.Count; i++)
        {
            messages[i] = CreateMessage(messageView[i]);
        }
        
        return messages;
    }
    
    /// <inheritdoc />
    public void Nack(Message message)
    {
        // No-op for RocketMQ: invisibility timeout will expire and message will become available for redelivery
    }

    /// <inheritdoc />
    public Task NackAsync(Message message, CancellationToken cancellationToken = default)
    {
        // No-op for RocketMQ: invisibility timeout will expire and message will become available for redelivery
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public bool Reject(Message message, MessageRejectionReason? reason)
        => BrighterAsyncContext.Run(() => RejectAsync(message, reason));


    /// <inheritdoc />
    public async Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var handler) || handler is not MessageView view)
        {
            return false;
        }

        Log.RejectingMessage(s_logger, message.Id);

        if (_deadLetterRoutingKey == null && _invalidMessageRoutingKey == null)
        {
            if (reason != null)
                Log.NoChannelsConfiguredForRejection(s_logger, message.Id, reason.RejectionReason.ToString());

            await consumer.Ack(view);
            return true;
        }

        var rejectionReason = reason?.RejectionReason ?? RejectionReason.None;

        try
        {
            RefreshMetadata(message, reason);

            var (routingKey, shouldRoute, isFallingBackToDlq) = DetermineRejectionRoute(rejectionReason);

            RocketMqMessageProducer? producer = null;
            if (shouldRoute)
            {
                message.Header.Topic = routingKey!;
                if (isFallingBackToDlq)
                    Log.FallingBackToDlq(s_logger, message.Id);

                producer = await GetProducerForRouteAsync(routingKey!);
            }

            if (producer != null)
            {
                await producer.SendAsync(message, cancellationToken);
                Log.MessageSentToRejectionChannel(s_logger, message.Id, rejectionReason.ToString());
            }
            else
            {
                Log.NoChannelsConfiguredForRejection(s_logger, message.Id, rejectionReason.ToString());
            }
        }
        catch (Exception ex)
        {
            Log.ErrorSendingToRejectionChannel(s_logger, ex, message.Id, rejectionReason.ToString());
            return true;
        }
        finally
        {
            await AckSourceMessageSafeAsync(view);
        }

        return true;
    }
    
    /// <inheritdoc />
    public bool Requeue(Message message, TimeSpan? delay = null)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var handler) || handler is not MessageView view)
        {
            return false;
        }
        
        // Waiting for next RocketMQ C# version, due an issue on ChangeInvisibleDuration
        // consumer.ChangeInvisibleDuration(view, TimeSpan.Zero);
        return true;
    }

    /// <inheritdoc />
    public Task<bool> RequeueAsync(Message message, TimeSpan? delay = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Requeue(message, delay));

    /// <summary>
    /// Acknowledges the source message, returning <c>false</c> on failure so that an ACK
    /// exception in a <c>finally</c> block cannot mask the DLQ send result.
    /// On failure the message will reappear after the invisibility timeout as a safety net.
    /// </summary>
    private async Task<bool> AckSourceMessageSafeAsync(MessageView view)
    {
        try
        {
            await consumer.Ack(view);
            return true;
        }
        catch (Exception ackEx)
        {
            Log.ErrorAckingSourceMessage(s_logger, ackEx);
            return false;
        }
    }

    private async Task<RocketMqMessageProducer?> GetProducerForRouteAsync(RoutingKey routingKey)
    {
        if (routingKey == _invalidMessageRoutingKey)
            return _invalidMessageProducer ??= await CreateProducerAsync(_invalidMessageRoutingKey);
        if (routingKey == _deadLetterRoutingKey)
            return _deadLetterProducer ??= await CreateProducerAsync(_deadLetterRoutingKey);
        return null;
    }

    private async Task<RocketMqMessageProducer?> CreateProducerAsync(RoutingKey? routingKey)
    {
        if (routingKey == null || _connection == null)
            return null;

        try
        {
            var rocketProducer = await new Producer.Builder()
                .SetClientConfig(_connection.ClientConfig)
                .SetMaxAttempts(_connection.MaxAttempts)
                .SetTopics(routingKey.Value)
                .Build();

            return new RocketMqMessageProducer(
                _connection,
                rocketProducer,
                new RocketMqPublication { Topic = routingKey });
        }
        catch (Exception ex)
        {
            Log.ErrorCreatingProducer(s_logger, ex, routingKey.Value);
            return null;
        }
    }

    private static void RefreshMetadata(Message message, MessageRejectionReason? reason)
    {
        message.Header.Bag["originalTopic"] = message.Header.Topic.Value;
        message.Header.Bag["rejectionTimestamp"] = DateTimeOffset.UtcNow.ToString("o");
        message.Header.Bag["originalMessageType"] = message.Header.MessageType.ToString();

        if (reason == null) return;

        message.Header.Bag["rejectionReason"] = reason.RejectionReason.ToString();
        if (!string.IsNullOrEmpty(reason.Description))
            message.Header.Bag["rejectionMessage"] = reason.Description ?? string.Empty;
    }

    private (RoutingKey? routingKey, bool foundProducer, bool isFallingBackToDlq) DetermineRejectionRoute(
        RejectionReason rejectionReason)
    {
        switch (rejectionReason)
        {
            case RejectionReason.Unacceptable:
                if (_invalidMessageRoutingKey != null)
                    return (_invalidMessageRoutingKey, true, false);
                if (_deadLetterRoutingKey != null)
                    return (_deadLetterRoutingKey, true, true);
                return (null, false, false);

            case RejectionReason.DeliveryError:
            case RejectionReason.None:
            default:
                if (_deadLetterRoutingKey != null)
                    return (_deadLetterRoutingKey, true, false);
                return (null, false, false);
        }
    }

    private static Message CreateMessage(MessageView message)
    {
        var topic = ReadTopic(message);
        var messageId = ReadMessageId(message);
        var timeStamp = ReadTimeStamp(message);
        var messageType = ReadMessageType(message);
        var correlationId = ReadCorrelationId(message);
        var partitionKey = ReadPartitionKey(message);
        var replyTo = ReadReplyTo(message);
        var contentType = ReadContentType(message) ?? new ContentType("plain/text");
        var handledCount = ReadHandledCount(message);
        var delay = ReadDelay(message);
        var source = ReadSource(message);
        var specVersion = ReadSpecVersion(message);
        var type = ReadType(message);
        var dateSchema = ReadDataSchema(message);
        var subject = ReadSubject(message);
        var dataRef = ReadDataRef(message);
        var traceParent = ReadTraceParent(message);
        var traceState = ReadTraceState(message);
        var baggage = ReadBaggage(message);
        
        var header = new MessageHeader(
            messageId: messageId,
            topic: topic,
            messageType,
            source: source,
            type: type,
            timeStamp: timeStamp,
            contentType: contentType,
            correlationId: correlationId,
            replyTo: replyTo,
            partitionKey: partitionKey,
            handledCount: handledCount,
            dataSchema: dateSchema,
            subject: subject,
            delayed: delay,
            traceParent: traceParent,
            traceState: traceState
        )
        {
            DataRef = dataRef,
            SpecVersion = specVersion,
            Baggage = baggage
        };

        foreach (var property in message.Properties)
        {
            header.Bag[property.Key] = property.Value;
        }

        header.Bag["ReceiptHandle"] = message;
        
        var body = new MessageBody(message.Body, header.ContentType);
        
        return new Message(header, body);

        static RoutingKey ReadTopic(MessageView message) => new(message.Topic);
        static Id ReadMessageId(MessageView message)
        {
            return message.Properties.TryGetValue(HeaderNames.MessageId, out var messageId)
                ? Id.Create(messageId)
                : Id.Create(message.MessageId);
        }

        static DateTimeOffset ReadTimeStamp(MessageView message)
        {
            if (message.Properties.TryGetValue(HeaderNames.TimeStamp, out var timestamp) 
                && DateTimeOffset.TryParse(timestamp, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AdjustToUniversal, out var datetime))
            {
                return datetime;
            }
            
            if (message.DeliveryTimestamp != null)
            {
                return message.DeliveryTimestamp.Value;
            }

            return DateTimeOffset.UtcNow;
        }
        
        static MessageType ReadMessageType(MessageView message)
        {
            if (message.Properties.TryGetValue(HeaderNames.MessageType, out var type) && Enum.TryParse<MessageType>(type, true, out var messageType))
            {
                return messageType;
            }

            return MessageType.MT_EVENT;
        }

        static Id? ReadCorrelationId(MessageView message)
        {
            var val = message.Properties.GetValueOrDefault(HeaderNames.CorrelationId);
            if (string.IsNullOrEmpty(val))
            {
                return null;
            }

            return Id.Create(val);
        }

        static PartitionKey? ReadPartitionKey(MessageView message)
        {
            if (string.IsNullOrEmpty(message.MessageGroup))
            {
                return null;
            }
            
            return new PartitionKey(message.MessageGroup);
        }

        static RoutingKey? ReadReplyTo(MessageView message)
        {
            var val = message.Properties.GetValueOrDefault(HeaderNames.ReplyTo);
            if (string.IsNullOrEmpty(val))
            {
                return null;
            }

            return new RoutingKey(val);
        }

        static ContentType? ReadContentType(MessageView message)
        {
            var val = message.Properties.GetValueOrDefault(HeaderNames.DataContentType);
            if (!string.IsNullOrEmpty(val))
            {
                return new ContentType(val);
            }
            
            val = message.Properties.GetValueOrDefault(HeaderNames.ContentType);
            if (!string.IsNullOrEmpty(val))
            {
                return new ContentType(val);
            }

            return null;
        }

        static int ReadHandledCount(MessageView message)
        {
            if (message.Properties.TryGetValue(HeaderNames.HandledCount, out var handledCount) 
                && int.TryParse(handledCount, out var count))
            {
                return count;
            }

            return 0;
        }

        static TimeSpan ReadDelay(MessageView message)
        {
            if (message.Properties.TryGetValue(HeaderNames.HandledCount, out var delayString) 
                && TimeSpan.TryParse(delayString, out var delay))
            {
                return delay;
            }

            return TimeSpan.Zero;
        }
        
        static Uri ReadSource(MessageView message)
        {
            var val = message.Properties.GetValueOrDefault(HeaderNames.Source);
            if (!string.IsNullOrEmpty(val) && Uri.TryCreate(val, UriKind.RelativeOrAbsolute, out var source))
            {
                return source;
            }

            return new Uri(MessageHeader.DefaultSource);
        }
        
        static string ReadSpecVersion(MessageView message)
        {
            var val = message.Properties.GetValueOrDefault(HeaderNames.SpecVersion);
            if (!string.IsNullOrEmpty(val))
            {
                return val;
            }

            return MessageHeader.DefaultSpecVersion;
        }
        
        static CloudEventsType ReadType(MessageView message)
        {
            var val = message.Properties.GetValueOrDefault(HeaderNames.Type);
            return string.IsNullOrEmpty(val) ? CloudEventsType.Empty : new CloudEventsType(val);
        }
        
        static Uri? ReadDataSchema(MessageView message)
        {
            var val = message.Properties.GetValueOrDefault(HeaderNames.DataSchema);
            if (!string.IsNullOrEmpty(val) && Uri.TryCreate(val, UriKind.RelativeOrAbsolute, out var source))
            {
                return source;
            }

            return null;
        }
        
        static string? ReadSubject(MessageView message)
        {
            var val = message.Properties.GetValueOrDefault(HeaderNames.Subject);
            if (!string.IsNullOrEmpty(val))
            {
                return val;
            }

            return null;
        }
        
        static string? ReadDataRef(MessageView message)
        {
            var val = message.Properties.GetValueOrDefault(HeaderNames.DataRef);
            if (!string.IsNullOrEmpty(val))
            {
                return val;
            }

            return null;
        }
        
        static TraceParent? ReadTraceParent(MessageView message)
        {
            var val = message.Properties.GetValueOrDefault(HeaderNames.TraceParent);
            if (!string.IsNullOrEmpty(val))
            {
                return new TraceParent(val);
            }

            return null;
        }
        
        static TraceState? ReadTraceState(MessageView message)
        {
            var val = message.Properties.GetValueOrDefault(HeaderNames.TraceState);
            if (!string.IsNullOrEmpty(val))
            {
                return new TraceState(val);
            }

            return null;
        }
        
        static Baggage ReadBaggage(MessageView message)
        {
            var baggage = new Baggage();
            var val = message.Properties.GetValueOrDefault(HeaderNames.Baggage);
            if (!string.IsNullOrEmpty(val))
            {
                baggage.LoadBaggage(val);
            }

            return baggage;
        }
    }

    /// <inheritdoc />
    public void Dispose() => consumer.Dispose();

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await consumer.DisposeAsync();

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Information, "RocketMessageConsumer: Rejecting message {MessageId}")]
        public static partial void RejectingMessage(ILogger logger, string messageId);

        [LoggerMessage(LogLevel.Warning, "RocketMessageConsumer: No DLQ or invalid message channels configured for message {MessageId}, rejection reason: {RejectionReason}")]
        public static partial void NoChannelsConfiguredForRejection(ILogger logger, string messageId, string rejectionReason);

        [LoggerMessage(LogLevel.Information, "RocketMessageConsumer: Message {MessageId} sent to rejection channel, reason: {RejectionReason}")]
        public static partial void MessageSentToRejectionChannel(ILogger logger, string messageId, string rejectionReason);

        [LoggerMessage(LogLevel.Warning, "RocketMessageConsumer: Falling back to DLQ for message {MessageId}")]
        public static partial void FallingBackToDlq(ILogger logger, string messageId);

        [LoggerMessage(LogLevel.Error, "RocketMessageConsumer: Error sending message {MessageId} to rejection channel, reason: {RejectionReason}")]
        public static partial void ErrorSendingToRejectionChannel(ILogger logger, Exception ex, string messageId, string rejectionReason);

        [LoggerMessage(LogLevel.Error, "RocketMessageConsumer: Error acknowledging source message after rejection")]
        public static partial void ErrorAckingSourceMessage(ILogger logger, Exception ex);

        [LoggerMessage(LogLevel.Error, "RocketMessageConsumer: Error creating producer for routing key {RoutingKey}")]
        public static partial void ErrorCreatingProducer(ILogger logger, Exception ex, string routingKey);
    }
}
