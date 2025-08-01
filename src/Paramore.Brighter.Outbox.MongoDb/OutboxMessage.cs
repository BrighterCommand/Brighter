using System.Net.Mime;
using System.Text.Json;
using MongoDB.Bson.Serialization.Attributes;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MongoDb;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Outbox.MongoDb;

/// <summary>
/// The MongoDb outbox message
/// </summary>
public class OutboxMessage : IMongoDbCollectionTTL
{
    /// <summary>
    /// Initialize the new instance of <see cref="OutboxMessage"/>
    /// </summary>
    public OutboxMessage()
    {
    }

    /// <summary>
    /// Initialize the new instance of <see cref="OutboxMessage"/>
    /// </summary>
    /// <param name="message">The message to be store.</param>
    /// <param name="expireAfterSeconds">When it should be expired.</param>
    public OutboxMessage(Message message, long? expireAfterSeconds = null)
    {
        TimeStamp = message.Header.TimeStamp == DateTimeOffset.MinValue
            ? DateTimeOffset.UtcNow
            : message.Header.TimeStamp;
        Body = message.Body.Bytes;
        var bodyContentType = message.Body.ContentType is not null ? message.Body.ContentType.ToString() : MediaTypeNames.Text.Plain;
        var headerContentType = message.Header.ContentType.ToString();
        
        BodyContentType = bodyContentType;
        ContentType = headerContentType;
        CorrelationId = message.Header.CorrelationId.Value;
        HeaderBag = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
        MessageId = message.Id.Value;
        MessageType = message.Header.MessageType.ToString();
        PartitionKey = message.Header.PartitionKey.Value;
        ReplyTo = message.Header.ReplyTo?.Value;
        Topic = message.Header.Topic.Value;
        Source = message.Header.Source.ToString();
        EventType = message.Header.Type;
        SpecVersion = message.Header.SpecVersion;
        DataSchema = message.Header.DataSchema?.AbsoluteUri;
        DataRef = message.Header.DataRef;
        Baggage = message.Header.Baggage.ToString();
        TraceParent = message.Header.TraceParent?.Value;
        TraceState = message.Header.TraceState?.Value;
        Subject = message.Header.Subject;
        ExpireAfterSeconds = expireAfterSeconds;
    }


    /// <summary>
    /// The Id of the Message. Used as a Global Secondary Index
    /// </summary>
    [BsonId]
    public string MessageId { get; set; } = string.Empty;
    
    /// <summary>
    /// The W3C Baggage of the message, if any
    /// </summary>
    public string? Baggage { get; set; }
    
    /// <summary>
    /// The message body
    /// </summary>
    public byte[]? Body { get; set; }
    
    /// <summary>
    /// The message content type
    /// </summary>
# if NET472
    public string BodyContentType { get; set; } = "appllcation/json";
#else
    public string BodyContentType { get; set; } = MediaTypeNames.Application.Json; 
#endif    
    
    /// <summary>
    /// The body encoding
    /// </summary>
    public string? CharacterEncoding { get; set; }
    
    /// <summary>
    /// The message content type
    /// </summary>
    public string? ContentType { get; set; } 
    
    /// <summary>
    /// The correlation id.
    /// </summary>
    public string? CorrelationId { get; set; }
    
    /// <summary>
    /// The CloudEvents DataRef for a Claim Check of the message, if any
    /// </summary>
    public string? DataRef { get; set; }
    
    /// <summary>
    /// The Cloud Events Data Reference of the message, if any
    /// </summary>
    public string? DataSchema { get; set; }
    
    /// <summary>
    /// The <see cref="DateTimeOffset"/> of when the message was dispatched
    /// </summary>
    public DateTimeOffset? Dispatched { get; set; }
    
    /// <summary>
    ///  The CloudEvents type of the message, if any 
   /// </summary>
    public string? EventType { get; set; }
    
    /// <summary>
    /// The document TTL
    /// </summary>
    public long? ExpireAfterSeconds { get; set; }
    
    /// <summary>
    /// The message header
    /// </summary>
    public string? HeaderBag { get; set; }
    
    /// <summary>
    /// The type of message i.e., MT_COMMAND, MT_EVENT, etc. An enumeration rendered as a string
    /// </summary>
    public string MessageType { get; set; } = string.Empty;
    
    /// <summary>
    /// The message partition key
    /// </summary>
    public string? PartitionKey { get; set; }
    
    /// <summary>
    /// The reply to
    /// </summary>
    public string? ReplyTo { get; set; }
    
    /// <summary>
    ///  The CloudEvents Source of the message, if any
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// The CloudEvents specification version of the message, if any
    /// </summary>
    public string SpecVersion { get; set; } = "1.0";
   
   /// <summary>
   /// The CloudEvents Subject of the message, if any
   /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// The <see cref="DateTimeOffset"/> of the message was created
    /// </summary>
    public DateTimeOffset TimeStamp { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// The Topic the message was published to
    /// </summary>
    public string Topic { get; set; } = string.Empty;
    
   /// <summary>
   ///  The W3C TraceParent of the message, if any
   /// </summary>
    public string? TraceParent { get; set; }
   
   /// <summary>
   /// The W3C TraceState of the message, if any 
   /// </summary>
    public string? TraceState { get; set; }

    /// <summary>
    /// Convert the outbox message to <see cref="Message"/>
    /// </summary>
    /// <returns>New instance of <see cref="Message"/>.</returns>
    public Message ConvertToMessage()
    {
        var characterEncoding = CharacterEncoding != null
            ? (CharacterEncoding)Enum.Parse(typeof(CharacterEncoding), CharacterEncoding)
            : Brighter.CharacterEncoding.UTF8;
        var messageType = (MessageType)Enum.Parse(typeof(MessageType), MessageType);

        var header = new MessageHeader(
            messageId: MessageId,
            topic: new RoutingKey(Topic),
            messageType: messageType,
            timeStamp: TimeStamp,
            correlationId: CorrelationId is not null ? new Id(CorrelationId) : null,
            replyTo: ReplyTo == null ? RoutingKey.Empty : new RoutingKey(ReplyTo));

        if (!string.IsNullOrEmpty(PartitionKey))
        {
            header.PartitionKey = PartitionKey!;
        }

        if (!string.IsNullOrEmpty(ContentType))
        {
            header.ContentType = new ContentType(ContentType);
        }

        if (!string.IsNullOrEmpty(HeaderBag))
        {
            var bag = JsonSerializer.Deserialize<Dictionary<string, object>>(HeaderBag!,
                JsonSerialisationOptions.Options)!;
            foreach (var keyValue in bag)
            {
                header.Bag.Add(keyValue.Key, keyValue.Value);
            }
        }

        // restore additional header fields
        if (!string.IsNullOrEmpty(Source))
            header.Source = new Uri(Source!);
        if (!string.IsNullOrEmpty(EventType))
            header.Type = new CloudEventsType(EventType!);
        if (!string.IsNullOrEmpty(SpecVersion))
            header.SpecVersion = SpecVersion;
        if (!string.IsNullOrEmpty(DataSchema))
            header.DataSchema = new Uri(DataSchema!);
        if (!string.IsNullOrEmpty(DataRef))
            header.DataRef = DataRef;
        header.Delayed = TimeSpan.Zero;
        header.HandledCount = 0;
        if (!string.IsNullOrEmpty(Baggage))
        {
            var baggage = new Baggage();    
            baggage.LoadBaggage(Baggage);
            header.Baggage = baggage;
        }
        if (!string.IsNullOrEmpty(TraceParent))
            header.TraceParent = new TraceParent(TraceParent!);
        if (!string.IsNullOrEmpty(TraceState))
            header.TraceState = new TraceState(TraceState!);
        if (!string.IsNullOrEmpty(Subject))
            header.Subject = Subject;

        var bodyContentType = new ContentType(BodyContentType) { CharSet = nameof(characterEncoding) };

        var body = new MessageBody(Body, bodyContentType, characterEncoding);

        return new Message(header, body);
    }
}
