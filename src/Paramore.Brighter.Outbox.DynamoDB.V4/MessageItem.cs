using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Paramore.Brighter.JsonConverters;
using Baggage = Paramore.Brighter.Observability.Baggage;

namespace Paramore.Brighter.Outbox.DynamoDB.V4;

[DynamoDBTable("brighter_outbox")]
public class MessageItem
{
    /// <summary>
    /// The message body
    /// </summary>
    [DynamoDBProperty(typeof(MessageItemBodyConverter))]
    public byte[]? Body { get; set; }
        
    /// <summary>
    /// What is the character encoding of the body?
    /// </summary>
    public string? CharacterEncoding { get; set; }

    /// <summary>
    /// What is the content type of the message?
    /// </summary>
    [DynamoDBProperty]
    public string? ContentType { get; set; } 

    ///<summary>
    /// The correlation id of the message
    /// </summary>
    [DynamoDBProperty]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// The time at which the message was created, formatted as a string yyyy-MM-ddTHH:mm:ss.fffZ
    /// </summary>
    public string? CreatedAt { get; set; }

    /// <summary>
    /// The time at which the message was created, in ticks
    /// </summary>
    [DynamoDBProperty]
    public long? CreatedTime { get; set; }

    /// <summary>
    /// The time at which the message was created, in ticks. Null if the message has been dispatched.
    /// </summary>
    [DynamoDBGlobalSecondaryIndexRangeKey(indexName: "Outstanding")]
    [DynamoDBProperty]
    public long? OutstandingCreatedTime { get; set; }
        
    /// <summary>
    /// The <a href="https://cloudevents.io/">CloudEvents</a> data schema of the message, if any.
    /// </summary>
    public string? DataSchema { get; }
        
    /// <summary>
    /// The <a href="https://cloudevents.io/">CloudEvents</a> data ref for a claim check, if any.
    /// </summary>
    public string? DataRef { get; set; }
        
    /// <summary>
    ///  If the message is to be delayed before send, how long is it delayed for?
    /// </summary>
    public int DelayedMilliseconds { get; set; }

    /// <summary>
    /// The time at which the message was delivered, formatted as a string yyyy-MM-dd
    /// </summary>
    [DynamoDBProperty]
    public string? DeliveredAt { get; set; }

    /// <summary>
    /// The time that the message was delivered to the broker, in ticks
    /// </summary>
    [DynamoDBGlobalSecondaryIndexRangeKey(indexName: "Delivered")]
    [DynamoDBProperty]
    public long? DeliveryTime { get; set; }
        
    [DynamoDBProperty]
    public long? ExpiresAt { get; set; }

    /// <summary>
    /// If there have been multiple attempts to process a message, keeps a running total
    /// </summary>
    public  int HandledCount { get; set; }
        
    /// <summary>
    /// A JSON object representing a dictionary of additional properties set on the message
    /// </summary>
    public string? HeaderBag { get; set; }

    /// <summary>
    /// The ID of the Message. Used as a Global Secondary Index
    /// </summary>
    [DynamoDBHashKey]
    [DynamoDBProperty]
    public string? MessageId { get; set; }

    /// <summary>
    /// The type of message i.e., MT_COMMAND, MT_EVENT, etc. An enumeration rendered as a string
    /// </summary>
    public string? MessageType { get; set; }

    /// <summary>
    /// The partition key for the Kafka message
    /// </summary>
    [DynamoDBProperty]
    public string? PartitionKey { get; set; }

    /// <summary>
    /// If this is a conversation i.e. request-response, what is the reply channel
    /// </summary>
    [DynamoDBProperty]
    public string? ReplyTo { get; set; }

    /// <summary>
    /// The <a href="https://cloudevents.io/">CloudEvents</a> source 
    /// </summary>
    public string? Source { get; set; } 
        
    /// <summary>
    /// The SpecVersion of <a href="https://cloudevents.io/">CloudEvents</a>  
    /// </summary>
    public string SpecVersion { get; set; } = MessageHeader.DefaultSpecVersion;
        
    /// <summary>
    /// The <a href="https://cloudevents.io/">CloudEvents</a> subject of the message, if any.
    /// </summary>
    public string? Subject { get; set; }
        
    /// <summary>
    /// The Topic the message was published to
    /// </summary>
    /// 
    [DynamoDBGlobalSecondaryIndexHashKey("Delivered")]
    [DynamoDBProperty]
    public string? Topic { get; set; }
        
    /// <summary>
    /// The Topic suffixed with the shard number
    /// </summary>
    [DynamoDBGlobalSecondaryIndexHashKey("Outstanding")]
    [DynamoDBProperty]
    public string? TopicShard { get; set; }

    /// <summary>
    /// What is the W3C Trace Parent of the span publishing the message?
    /// </summary>
    public string? TraceParent { get; }
        
    /// <summary>
    /// What is the W3C Trace State of the span publishing the message?
    /// </summary>
    public string? TraceState { get; }

    /// <summary>
    /// What is the <a href="https://cloudevents.io/">CloudEvents</a> Type of the message? 
    /// </summary>
    public string? Type { get; set; }

    public string? Baggage { get; }

    public MessageItem()
    {
        /*Deserialization*/
    }

    public MessageItem(Message message, int shard = 0, long? expiresAt = null)
    {
        var date = message.Header.TimeStamp == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow : message.Header.TimeStamp;

        Body = message.Body.Bytes;
        var contentType = message.Header.ContentType ?? new ContentType(MediaTypeNames.Text.Plain);

        ContentType = contentType.ToString();
        CorrelationId = message.Header.CorrelationId.ToString();
        CharacterEncoding = message.Body.CharacterEncoding.ToString();
        CreatedAt = date.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        CreatedTime = date.Ticks;
        OutstandingCreatedTime = date.Ticks;
        DeliveryTime = null;
        HeaderBag = JsonSerializer.Serialize(message.Header.Bag);
        MessageId = message.Id.ToString();
        MessageType = message.Header.MessageType.ToString();
        PartitionKey = message.Header.PartitionKey;
        ReplyTo = message.Header.ReplyTo?.Value;
        Topic = message.Header.Topic;
        TopicShard = $"{Topic}_{shard}";
        ExpiresAt = expiresAt;
        HandledCount = 0; //An outbox message is always created with a HandledCount of 0
        DelayedMilliseconds = 0; //An outbox message is always created with a DelayedMilliseconds of 0
        Type = message.Header.Type;
        SpecVersion = message.Header.SpecVersion;
        Subject = message.Header.Subject;
        Source = message.Header.Source.AbsoluteUri;
        DataSchema = message.Header.DataSchema?.AbsoluteUri;
        DataRef = message.Header.DataRef;
        TraceParent = message.Header.TraceParent?.Value;
        TraceState = message.Header.TraceState?.Value;
        Baggage =message.Header.Baggage.ToString();
    }

    public Message ConvertToMessage()
    {
        var characterEncoding = CharacterEncoding != null ? (CharacterEncoding) Enum.Parse(typeof(CharacterEncoding), CharacterEncoding) : Brighter.CharacterEncoding.UTF8;
        var correlationId = CorrelationId;
        var bag = JsonSerializer.Deserialize<Dictionary<string, object>>(HeaderBag!, JsonSerialisationOptions.Options);
        var messageId = MessageId;
        var messageType = (MessageType)Enum.Parse(typeof(MessageType), MessageType!);
        var timestamp = new DateTime(CreatedTime ?? DateTime.UtcNow.Ticks, DateTimeKind.Utc);
        var contentType = ContentType is not null ? new ContentType(ContentType) : new ContentType(MediaTypeNames.Text.Plain);
        var baggage = new Baggage();
        baggage.LoadBaggage(Baggage);
            
        var header = new MessageHeader(
            messageId: Id.Create(messageId),
            topic: Topic is not null ? new RoutingKey(Topic) :RoutingKey.Empty,
            messageType: messageType,
            timeStamp: timestamp,
            correlationId: Id.Create(correlationId),
            replyTo: ReplyTo is not null ? new RoutingKey(ReplyTo) : RoutingKey.Empty,
            contentType: contentType, 
            partitionKey: PartitionKey is not null ? new PartitionKey(PartitionKey) : Paramore.Brighter.PartitionKey.Empty,
            handledCount: 0,    //we set to zero in the outbox
            delayed: TimeSpan.Zero,
            type:new CloudEventsType(Type ?? string.Empty),
            subject: Subject,
            source: !string.IsNullOrEmpty(Source) ? new Uri(Source) : new Uri("https://paramore.io"),
            dataSchema: !string.IsNullOrEmpty(DataSchema) ? new Uri(DataSchema) : new Uri("https://goparamore.io"),
            traceParent: !string.IsNullOrEmpty(TraceParent) ? new TraceParent(TraceParent!) : Brighter.TraceParent.Empty,
            traceState: !string.IsNullOrEmpty(TraceState) ? new TraceState(TraceState!) : Brighter.TraceState.Empty,
            baggage: baggage
        )
        {
            DataRef = DataRef
        };

        if (bag == null) return new Message(header, new MessageBody(Body, contentType, characterEncoding));
            
        foreach (var keyValue in bag)
            header.Bag.Add(keyValue.Key, keyValue.Value);

        return new Message(header, new MessageBody(Body, contentType, characterEncoding));
    }
}

public class MessageItemBodyConverter : IPropertyConverter
{
    public DynamoDBEntry ToEntry(object? value)
    {
        byte[]? body = value as byte[];
        if (body == null) throw new ArgumentOutOfRangeException(nameof(value), "Expected the body to be a byte array");

        DynamoDBEntry entry = new Primitive
        {
            Value = body,
            Type = DynamoDBEntryType.Binary
        };
            
        return entry;
    }

    public object FromEntry(DynamoDBEntry entry)
    {
        byte[] data = [];
        Primitive? primitive = entry as Primitive; 
        if (primitive?.Value is byte[] bytes)
            data = bytes;
        else if (primitive?.Value is string text)    //for historical data that used UTF-8 strings
            data = Encoding.UTF8.GetBytes(text);
        if (primitive == null || !(primitive.Value is string || primitive.Value is byte[]))
            throw new ArgumentOutOfRangeException(nameof(entry), "Expected Dynamo to have stored a byte array");
            
        return data;
    }
}
