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
    /// <value>The <see cref="string"/>value of the message id</value>
    [BsonId]
    public string MessageId { get; set; } = string.Empty;
    
    /// <summary>
    /// The W3C Baggage of the message, if any
    /// </summary>
    /// <value>The <see cref="string"/>value of the baggage in W3C format</value>
    public string? Baggage { get; set; }
    
    /// <summary>
    /// The message body
    /// </summary>
    /// <value>An array of <see cref="byte"/> representing the payload of the message</value>
    public byte[]? Body { get; set; }
    
    /// <summary>
    /// The message content type - defaults to "application/json"
    /// </summary>
    /// <value>The <see cref="string"/>of the body content type</value>
# if NET472
    public string BodyContentType { get; set; } = "appllcation/json";
#else
    public string BodyContentType { get; set; } = MediaTypeNames.Application.Json; 
#endif    
    
    /// <summary>
    /// The body's character encoding; defaults to utf-8
    /// </summary>
    /// <value>The character encoding expressed as a <see cref="string"/>s</value>
    public string? CharacterEncoding { get; set; }
    
    /// <summary>
    /// The body content type; usually the same as the body content type, but may vary if body has been transformed in pipeline
    /// </summary>
    /// <value>The <see cref="string"/>of the body content type</value>
    public string? ContentType { get; set; } 
    
    /// <summary>
    /// The correlation id.
    /// </summary>
    /// <value>The <see cref="stirng"/> with the correlation id.</value>
    public string? CorrelationId { get; set; }
    
    /// <summary>
    /// The CloudEvents DataRef for a Claim Check of the message, if any
    /// </summary>
    /// <value>The <see cref="string"/> that acts as a claim check for the message</value>
    public string? DataRef { get; set; }
    
    /// <summary>
    /// The Cloud Events Data Reference of the message, if any, a uri
    /// </summary>
    /// <value>The <see cref="string"/> that indicates the uri of the data schema</value>
    public string? DataSchema { get; set; }
    
    /// <summary>
    /// The age of a dispatched message  i.e. 5 mins ago
    /// </summary>
    /// <value>The <see cref="DateTimeOffset"/> that represents the age of a dispatched message</value>
    public DateTimeOffset? Dispatched { get; set; }
    
    /// <summary>
    ///  The CloudEvents type of the message, if any 
   /// </summary>
   /// <value>The <see cref="string"/> type of a message on a channel</value>
    public string? EventType { get; set; }
    
    /// <summary>
    /// DynamoDb can auto-delete messages using TTL, how long should we keep an Outbox entry
    /// </summary>
    /// <value>The <see cref="long"/> number of seconds  a document should live</value>
    public long? ExpireAfterSeconds { get; set; }
    
    /// <summary>
    /// The message header
    /// </summary>
    ///<value>The contents of the bag of user defined properties expressed as a <see cref="string"/></value> 
    public string? HeaderBag { get; set; }
    
    /// <summary>
    /// The identifier for the instance of a workflow that the message is associated with
    /// </summary>
    /// <value>The <see cref="string"/> identifier for a job</value>
    public string? JobId { get; set; } 
    
    /// <summary>
    /// The type of message i.e., MT_COMMAND, MT_EVENT, etc. An enumeration rendered as a string
    /// </summary>
    /// <value>The <see cref="string "/> for the type of message </value>
    public string MessageType { get; set; } = string.Empty;
    
    /// <summary>
    /// The message partition key
    /// </summary>
    /// <value>The partition key <see cref="string"/></value>
    public string? PartitionKey { get; set; }
    
    /// <summary>
    /// Used to indicate the channel that the receiver should send any reply to
    /// </summary>
    /// <value>The reply to channel expressed as a <see cref="string"/></value>
    public string? ReplyTo { get; set; }
    
    /// <summary>
    ///  The CloudEvents Source of the message, if any
    /// </summary>
    ///<value>The <see cref="string"/> indicating the source that produced the message</value>
    public string? Source { get; set; }

    /// <summary>
    /// The CloudEvents specification version of the message; defaults to 1.0
    /// </summary>
    /// <value>The <see cref="string"/> with the Cloud Events Version</value>
    public string SpecVersion { get; set; } = "1.0";
   
   /// <summary>
   /// The CloudEvents Subject of the message, if any
   /// </summary>
   /// <value>The <see cref="string"/> with the subject</value>
    public string? Subject { get; set; }

    /// <summary>
    /// When was the message created 
    /// </summary>
    /// <value>The <see cref="DateTimeOffset"/> of the message was created </value>
    public DateTimeOffset TimeStamp { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// The Topic the message was published to
    /// </summary>
    /// <value>The <see cref="string"/> topic.</value>
    public string Topic { get; set; } = string.Empty;
    
   /// <summary>
   ///  The W3C TraceParent of the message, if any
   /// </summary>
   /// <value>The <see cref="string"/> with the W3C Trace Parent header</value>
    public string? TraceParent { get; set; }
   
   /// <summary>
   /// The W3C TraceState of the message, if any 
   /// </summary>
   /// <value>The <see cref="string"/> with the W3C Trace State</value>
    public string? TraceState { get; set; }
   
   /// <summary>
   /// The Workflow that this message is associated with
   /// </summary>
   /// <value>The <see cref="string"/> id of the workflo</value>
   public string? WorkflowId { get; set; } 

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
