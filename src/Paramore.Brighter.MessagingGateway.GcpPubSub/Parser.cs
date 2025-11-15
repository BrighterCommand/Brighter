using System.Net.Mime;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

internal static class Parser
{
    private static readonly HashSet<string> s_ignoreHeaders = new(StringComparer.InvariantCultureIgnoreCase)
    {
        HeaderNames.MessageType,
        HeaderNames.CorrelationId,
        HeaderNames.Topic,
        HeaderNames.HandledCount,
        HeaderNames.Id,
        HeaderNames.ContentType,
        HeaderNames.Timestamp,
        HeaderNames.ReplyTo,
        HeaderNames.Subject,
        HeaderNames.SpecVersion,
        HeaderNames.Type,
        HeaderNames.Source,
        HeaderNames.DataSchema,
        HeaderNames.TraceParent,
        HeaderNames.TraceState,
        HeaderNames.Baggage
    };


    public static Message ToBrighterMessage(GcpStreamMessage receivedMessage)
    {
        var partitionKey = receivedMessage.Message.OrderingKey;
        var topic = ReadTopic(receivedMessage.Message.Attributes);
        var messageId = ReadMessageId(receivedMessage.Message.Attributes);
        var handleCount = ReadHandleCount(receivedMessage.Message.Attributes);
        var contentType = ReadContentType(receivedMessage.Message.Attributes);
        var messageType = ReadMessageType(receivedMessage.Message.Attributes);
        var timestamp = ReadTimestamp(receivedMessage.Message.Attributes);
        var replyTo = ReadReplyTo(receivedMessage.Message.Attributes);
        var correlationId = ReadCorrelationId(receivedMessage.Message.Attributes);
        var subject = ReadSubject(receivedMessage.Message.Attributes);
        var type = ReadType(receivedMessage.Message.Attributes);
        var source = ReadSource(receivedMessage.Message.Attributes);
        var dataSchema = ReadDataSchema(receivedMessage.Message.Attributes);
        var traceParent = ReadTraceParent(receivedMessage.Message.Attributes);
        var traceState = ReadTraceState(receivedMessage.Message.Attributes);
        var baggage = ReadBaggage(receivedMessage.Message.Attributes);

        var messageHeader = new MessageHeader(
            messageId: messageId,
            topic: topic,
            messageType: messageType,
            source: source,
            type: type,
            timeStamp: timestamp,
            correlationId: correlationId,
            replyTo: replyTo,
            contentType: contentType,
            handledCount: handleCount,
            dataSchema: dataSchema,
            subject: subject,
            delayed: TimeSpan.Zero,
            partitionKey: partitionKey,
            baggage: baggage,
            traceParent: traceParent,
            traceState: traceState
        );

        foreach (var header in receivedMessage.Message.Attributes
                     .Where(x => !s_ignoreHeaders.Contains(x.Key)))
        {
            messageHeader.Bag[header.Key] = header.Value;
        }

        messageHeader.Bag["ReceiptHandle"] = receivedMessage;

        var body = new MessageBody(receivedMessage.Message.Data.ToByteArray());
        return new Message(messageHeader, body);
    }
    
    public static Message ToBrighterMessage(ReceivedMessage receivedMessage)
    {
        var receiptHandle = receivedMessage.AckId;
        var partitionKey = receivedMessage.Message.OrderingKey;
        var topic = ReadTopic(receivedMessage.Message.Attributes);
        var messageId = ReadMessageId(receivedMessage.Message.Attributes);
        var handleCount = ReadHandleCount(receivedMessage.Message.Attributes);
        var contentType = ReadContentType(receivedMessage.Message.Attributes);
        var messageType = ReadMessageType(receivedMessage.Message.Attributes);
        var timestamp = ReadTimestamp(receivedMessage.Message.Attributes);
        var replyTo = ReadReplyTo(receivedMessage.Message.Attributes);
        var correlationId = ReadCorrelationId(receivedMessage.Message.Attributes);
        var subject = ReadSubject(receivedMessage.Message.Attributes);
        var type = ReadType(receivedMessage.Message.Attributes);
        var source = ReadSource(receivedMessage.Message.Attributes);
        var dataSchema = ReadDataSchema(receivedMessage.Message.Attributes);
        var traceParent = ReadTraceParent(receivedMessage.Message.Attributes);
        var traceState = ReadTraceState(receivedMessage.Message.Attributes);
        var baggage = ReadBaggage(receivedMessage.Message.Attributes);

        var messageHeader = new MessageHeader(
            messageId: messageId,
            topic: topic,
            messageType: messageType,
            source: source,
            type: type,
            timeStamp: timestamp,
            correlationId: correlationId,
            replyTo: replyTo,
            contentType: contentType,
            handledCount: handleCount,
            dataSchema: dataSchema,
            subject: subject,
            delayed: TimeSpan.Zero,
            partitionKey: partitionKey,
            traceParent: traceParent,
            traceState: traceState,
            baggage: baggage
        );

        foreach (var header in receivedMessage.Message.Attributes
                     .Where(x => !s_ignoreHeaders.Contains(x.Key)))
        {
            messageHeader.Bag[header.Key] = header.Value;
        }

        messageHeader.Bag["ReceiptHandle"] = receiptHandle;

        var body = new MessageBody(receivedMessage.Message.Data.ToByteArray());

        return new Message(messageHeader, body);
    }

    private static RoutingKey ReadTopic(MapField<string, string> attributes)
    {
        if (attributes.TryGetValue(HeaderNames.Topic, out var topic))
        {
            return new RoutingKey(topic);
        }

        return RoutingKey.Empty;
    }

    private static string ReadMessageId(MapField<string, string> attributes)
    {
        if (attributes.TryGetValue(HeaderNames.Id, out var id))
        {
            return id;
        }

        return Guid.NewGuid().ToString();
    }

    private static ContentType ReadContentType(MapField<string, string> attributes)
    {
        if (attributes.TryGetValue(HeaderNames.ContentType, out var contentType))
        {
            return new ContentType(contentType);
        }

        return new ContentType("text/plain");
    }

    private static int ReadHandleCount(MapField<string, string> attributes)
    {
        if (attributes.TryGetValue(HeaderNames.HandledCount, out var val) && int.TryParse(val, out var handleCount))
        {
            return handleCount;
        }

        return 0;
    }

    private static MessageType ReadMessageType(MapField<string, string> attributes)
    {
        if (attributes.TryGetValue(HeaderNames.MessageType, out var val) &&
            Enum.TryParse<MessageType>(val, out var messageType))
        {
            return messageType;
        }

        return MessageType.MT_EVENT;
    }

    private static DateTimeOffset ReadTimestamp(MapField<string, string> attributes)
    {
        if (attributes.TryGetValue(HeaderNames.Timestamp, out var val) &&
            DateTimeOffset.TryParse(val, out var timestamp))
        {
            return timestamp;
        }

        return DateTimeOffset.UtcNow;
    }

    private static RoutingKey ReadReplyTo(MapField<string, string> attributes)
    {
        if (attributes.TryGetValue(HeaderNames.ReplyTo, out var replyTo))
        {
            return new RoutingKey(replyTo);
        }

        return RoutingKey.Empty;
    }

    private static string ReadCorrelationId(MapField<string, string> attributes)
    {
        if (attributes.TryGetValue(HeaderNames.CorrelationId, out var correlationId))
        {
            return correlationId;
        }

        return string.Empty;
    }

    private static string ReadSubject(MapField<string, string> attributes)
    {
        if (attributes.TryGetValue(HeaderNames.Subject, out var subject))
        {
            return subject;
        }

        return string.Empty;
    }

    private static CloudEventsType ReadType(MapField<string, string> attributes)
    {
        if (attributes.TryGetValue(HeaderNames.Type, out var type))
        {
            return new CloudEventsType(type);
        }

        return CloudEventsType.Empty;
    }

    private static Uri? ReadSource(MapField<string, string> attributes)
    {
        if (attributes.TryGetValue(HeaderNames.Source, out var val)
            && Uri.TryCreate(val, UriKind.RelativeOrAbsolute, out var source))
        {
            return source;
        }

        return null;
    }

    private static Uri? ReadDataSchema(MapField<string, string> attributes)
    {
        if (attributes.TryGetValue(HeaderNames.DataSchema, out var val)
            && Uri.TryCreate(val, UriKind.RelativeOrAbsolute, out var dataSchema))
        {
            return dataSchema;
        }

        return null;
    }
    
    private static TraceParent? ReadTraceParent(MapField<string, string> attributes)
    {
        if (attributes.TryGetValue(HeaderNames.TraceParent, out var traceParent))
        {
            return new TraceParent(traceParent);
        }

        return null;
    }
    
    private static TraceState? ReadTraceState(MapField<string, string> attributes)
    {
        if (attributes.TryGetValue(HeaderNames.TraceState, out var traceState))
        {
            return new TraceState(traceState);
        }

        return null;
    }
    
    private static Baggage ReadBaggage(MapField<string, string> attributes)
    {
        var baggage = new Baggage();
        if (attributes.TryGetValue(HeaderNames.Baggage, out var val))
        {
            baggage.LoadBaggage(val);
        }

        return baggage;
    }

    public static PubsubMessage ToPubSubMessage(Message message)
    {
        var pubSubMessage = new PubsubMessage
        {
            Data = ByteString.CopyFrom(message.Body.Bytes), OrderingKey = message.Header.PartitionKey
        };

        AddHeaders(pubSubMessage.Attributes, message);
        return pubSubMessage;
    }

    private static void AddHeaders(MapField<string, string> headers, Message message)
    {
        headers.Add(HeaderNames.Id, message.Header.MessageId);
        headers.Add(HeaderNames.Topic, message.Header.Topic);
        headers.Add(HeaderNames.HandledCount, message.Header.HandledCount.ToString());
        headers.Add(HeaderNames.MessageType, message.Header.MessageType.ToString());
        headers.Add(HeaderNames.SpecVersion, message.Header.SpecVersion);
        headers.Add(HeaderNames.Source, message.Header.Source.ToString());
        headers.Add(HeaderNames.Timestamp, message.Header.TimeStamp.ToRfc3339());
        headers.Add(HeaderNames.ContentType, message.Header.ContentType.ToString());
        headers.Add(HeaderNames.Baggage, message.Header.Baggage.ToString());

        if (message.Header.Type != CloudEventsType.Empty)
        {
            headers.Add(HeaderNames.Type, message.Header.Type);
        }

        if (!string.IsNullOrEmpty(message.Header.CorrelationId))
        {
            headers.Add(HeaderNames.CorrelationId, message.Header.CorrelationId);
        }

        if (!RoutingKey.IsNullOrEmpty(message.Header.ReplyTo))
        {
            headers.Add(HeaderNames.ReplyTo, message.Header.ReplyTo!);
        }

        if (!string.IsNullOrEmpty(message.Header.Subject))
        {
            headers.Add(HeaderNames.Subject, message.Header.Subject!);
        }

        if (message.Header.DataSchema != null)
        {
            headers.Add(HeaderNames.DataSchema, message.Header.DataSchema.ToString());
        }

        if (!TraceParent.IsNullOrEmpty(message.Header.TraceParent))
        {
            headers.Add(HeaderNames.TraceParent, message.Header.TraceParent.Value);
        }

        if (!TraceState.IsNullOrEmpty(message.Header.TraceState))
        {
            headers.Add(HeaderNames.TraceState, message.Header.TraceState.Value);
        }

        message.Header.Bag.Each(header =>
        {
            if (!headers.ContainsKey(header.Key))
            {
                headers.Add(header.Key, header.Value.ToString()!);
            }
        });
    }
}
