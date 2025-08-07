using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Org.Apache.Rocketmq;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.RocketMQ;

/// <summary>
/// RocketMQ message consumer implementation for Brighter.
/// Integrates RocketMQ's consumer group pattern and message filtering capabilities.
/// </summary>
public class RocketMessageConsumer(SimpleConsumer consumer, 
    int bufferSize, 
    TimeSpan invisibilityTimeout)
    : IAmAMessageConsumerAsync, IAmAMessageConsumerSync
{
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
    public Task<bool> RejectAsync(Message message, CancellationToken cancellationToken = default) 
        => Task.FromResult(Reject(message));

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
    public Task<bool> RequeueAsync(Message message, TimeSpan? delay = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Requeue(message, delay));

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
            delayed: delay
        )
        {
            DataRef = dataRef,
            SpecVersion = specVersion,
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
    }

    /// <inheritdoc />
    public void Acknowledge(Message message) 
        => BrighterAsyncContext.Run(async () => await AcknowledgeAsync(message));

    /// <inheritdoc />
    public bool Reject(Message message) => Requeue(message);

    /// <inheritdoc />
    public void Purge() 
        => BrighterAsyncContext.Run(async () => await PurgeAsync());

    /// <inheritdoc />
    public Message[] Receive(TimeSpan? timeOut = null)
        => BrighterAsyncContext.Run(async () => await ReceiveAsync(timeOut));

    
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
    public void Dispose() => consumer.Dispose();
    
    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await consumer.DisposeAsync();
}
