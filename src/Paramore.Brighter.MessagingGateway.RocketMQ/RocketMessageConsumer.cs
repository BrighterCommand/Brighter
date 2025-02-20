using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Org.Apache.Rocketmq;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.RocketMQ;

/// <summary>
/// The RocketMQ consumer
/// </summary>
public class RocketMessageConsumer : IAmAMessageConsumerAsync, IAmAMessageConsumerSync
{
    private readonly ConcurrentDictionary<Message, MessageView> _queue = new();
    private readonly SimpleConsumer _consumer;
    private readonly int _bufferSize;
    private readonly TimeSpan _invisibilityTimeout;

    /// <summary>
    /// Initialize <see cref="RocketMessageConsumer"/>
    /// </summary>
    /// <param name="consumer">The <see cref="SimpleConsumer"/>.</param>
    /// <param name="bufferSize">The consumer batch size</param>
    /// <param name="invisibilityTimeout">The invisible timeout for getting a message</param>
    public RocketMessageConsumer(SimpleConsumer consumer, int bufferSize, TimeSpan invisibilityTimeout)
    {
        _consumer = consumer;
        _bufferSize = bufferSize;
        _invisibilityTimeout = invisibilityTimeout;
    }

    /// <inheritdoc />
    public async Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (_queue.TryRemove(message, out var view))
        {
            await _consumer.Ack(view);
        }
    }
    
    /// <inheritdoc />
    public Task RejectAsync(Message message, CancellationToken cancellationToken = default(CancellationToken))
    {
        Reject(message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        foreach (var queue in _queue)
        {
            await _consumer.Ack(queue.Value);
        }
        
        _queue.Clear();
        
        while (true)
        {
            var messages = await _consumer.Receive(_bufferSize, _invisibilityTimeout);
            if (messages == null || messages.Count == 0)
            {
                break;
            }
            
            await messages.EachAsync(async message => await _consumer.Ack(message));
        }
    }

    /// <inheritdoc />
    public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        var messageView = await _consumer.Receive(_bufferSize, _invisibilityTimeout);
        if (messageView == null || messageView.Count == 0)
        {
            return [new Message()];
        }
        
        var messages = new Message[messageView.Count];
        for (int i = 0; i < messageView.Count; i++)
        {
            messages[i] = CreateMessage(messageView[i]);
            _queue[messages[i]] = messageView[i];
        }
        
        return messages;
    }

    /// <summary>
    /// Requeues the specified message.
    /// It's not support by RocketMQ
    /// </summary>
    /// <param name="message"></param>
    /// <param name="delay"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>False as no requeue support on RocketMQ</returns>
    public Task<bool> RequeueAsync(Message message, TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
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
        var contentType = ReadContentType(message);
        var handledCount = ReadHandledCount(message);
        var delay = ReadDelay(message);

        if (string.IsNullOrEmpty(topic) || string.IsNullOrEmpty(messageId) || timeStamp == DateTime.MinValue)
        {
            return new Message(
                new MessageHeader(messageId, new RoutingKey(topic), MessageType.MT_UNACCEPTABLE),
                new MessageBody(string.Empty));
        }
        
        var header = new MessageHeader(
            messageId: messageId,
            topic: new RoutingKey(topic),
            messageType,
            source: null,
            type: "",
            timeStamp: timeStamp,
            correlationId: correlationId,
            replyTo: string.IsNullOrEmpty(replyTo) ? RoutingKey.Empty : new RoutingKey(replyTo) ,
            contentType: contentType ?? "plain/text",
            partitionKey: string.IsNullOrEmpty(partitionKey) ? message.MessageGroup : partitionKey,
            handledCount: handledCount,
            dataSchema: null,
            subject: null,
            delayed: delay
        );

        foreach (var property in message.Properties)
        {
            header.Bag[property.Key] = property.Value;
        }
        
        var body = new MessageBody(message.Body, header.ContentType ?? "plain/text");
        
        return new Message(header, body);

        static string ReadTopic(MessageView message) => message.Topic;
        static string ReadMessageId(MessageView message) => message.Properties.TryGetValue(HeaderNames.MessageId, out var messageId) ? messageId : message.MessageId;
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
            if (message.Properties.TryGetValue(HeaderNames.MessageType, out var type))
            {
                return Enum.TryParse<MessageType>(type, true, out var messageType) ? messageType : MessageType.MT_UNACCEPTABLE;
            }

            return MessageType.MT_EVENT;
        }

        static string? ReadCorrelationId(MessageView message) => message.Properties.GetValueOrDefault(HeaderNames.CorrelationId);
        static string? ReadPartitionKey(MessageView message) => message.MessageGroup;
        static string? ReadReplyTo(MessageView message) => message.Properties.GetValueOrDefault(HeaderNames.ReplyTo);
        static string? ReadContentType(MessageView message) => message.Properties.GetValueOrDefault(HeaderNames.ContentType);
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
    }

    /// <inheritdoc />
    public void Acknowledge(Message message) 
        => BrighterAsyncContext.Run(async () => await AcknowledgeAsync(message));

    /// <inheritdoc />
    public void Reject(Message message)
    {
        if (_queue.TryRemove(message, out var view))
        {
            _consumer.ChangeInvisibleDuration(view, TimeSpan.Zero);
        }
    }

    /// <inheritdoc />
    public void Purge() 
        => BrighterAsyncContext.Run(async () => await PurgeAsync());

    /// <inheritdoc />
    public Message[] Receive(TimeSpan? timeOut = null)
        => BrighterAsyncContext.Run(async () => await ReceiveAsync(timeOut));

    /// <summary>
    /// Requeues the specified message.
    /// It's not support by RocketMQ
    /// </summary>
    /// <param name="message"></param>
    /// <param name="delay"></param>
    /// <returns>False as no requeue support on RocketMQ</returns>
    public bool Requeue(Message message, TimeSpan? delay = null)
        => false;
    
    /// <inheritdoc />
    public void Dispose() => _consumer.Dispose();
    
    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _consumer.DisposeAsync();
}
