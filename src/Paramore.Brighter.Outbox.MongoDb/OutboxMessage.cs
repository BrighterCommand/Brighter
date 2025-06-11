using System.Text.Json;
using MongoDB.Bson.Serialization.Attributes;
using Paramore.Brighter.MongoDb;

namespace Paramore.Brighter.Outbox.MongoDb;

/// <summary>
/// The MongoDb outbox message
/// </summary>
public class OutboxMessage : IMongoDbCollectionTTL
{
    /// <summary>
    /// Initialize new instance of <see cref="OutboxMessage"/>
    /// </summary>
    public OutboxMessage()
    {
    }

    /// <summary>
    /// Initialize new instance of <see cref="OutboxMessage"/>
    /// </summary>
    /// <param name="message">The message to be store.</param>
    /// <param name="expireAfterSeconds">When it should be expired.</param>
    public OutboxMessage(Message message, long? expireAfterSeconds = null)
    {
        TimeStamp = message.Header.TimeStamp == DateTimeOffset.MinValue
            ? DateTimeOffset.UtcNow
            : message.Header.TimeStamp;
        Body = message.Body.Bytes;
        BodyContentType = message.Body.ContentType;
        ContentType = message.Header.ContentType;
        CorrelationId = message.Header.CorrelationId;
        HeaderBag = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
        MessageId = message.Id;
        MessageType = message.Header.MessageType.ToString();
        PartitionKey = message.Header.PartitionKey;
        ReplyTo = message.Header.ReplyTo;
        Topic = message.Header.Topic;
        ExpireAfterSeconds = expireAfterSeconds;
    }

    /// <summary>
    /// The Id of the Message. Used as a Global Secondary Index
    /// </summary>
    [BsonId]
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// The Topic the message was published to
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// The type of message i.e. MT_COMMAND, MT_EVENT etc. An enumeration rendered as a string
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// The <see cref="DateTimeOffset"/> of the message was created
    /// </summary>
    public DateTimeOffset TimeStamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The correlation id.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// The reply to
    /// </summary>
    public string? ReplyTo { get; set; }

    /// <summary>
    /// The message content type
    /// </summary>
    public string? ContentType { get; set; } 

    /// <summary>
    /// The message content type
    /// </summary>
    public string BodyContentType { get; set; } = MessageBody.APPLICATION_JSON;

    /// <summary>
    /// The message partition key
    /// </summary>
    public string? PartitionKey { get; set; }

    /// <summary>
    /// The <see cref="DateTimeOffset"/> of when the message was dispatched
    /// </summary>
    public DateTimeOffset? Dispatched { get; set; }

    /// <summary>
    /// The message header
    /// </summary>
    public string? HeaderBag { get; set; }

    /// <summary>
    /// The message body
    /// </summary>
    public byte[]? Body { get; set; }

    /// <summary>
    /// The body encoding
    /// </summary>
    public string? CharacterEncoding { get; set; }

    /// <summary>
    /// The document TTL
    /// </summary>
    public long? ExpireAfterSeconds { get; set; }

    /// <summary>
    /// Convert the outbox message to <see cref="Message"/>
    /// </summary>
    /// <returns>New instance of <see cref="Message"/>.</returns>
    public Message ConvertToMessage()
    {
        //following type may be missing on older data
        var characterEncoding = CharacterEncoding != null
            ? (CharacterEncoding)Enum.Parse(typeof(CharacterEncoding), CharacterEncoding)
            : Brighter.CharacterEncoding.UTF8;
        var messageType = (MessageType)Enum.Parse(typeof(MessageType), MessageType);

        var header = new MessageHeader(
            messageId: MessageId,
            topic: new RoutingKey(Topic),
            messageType: messageType,
            timeStamp: TimeStamp,
            correlationId: CorrelationId,
            replyTo: ReplyTo == null ? RoutingKey.Empty : new RoutingKey(ReplyTo));

        if (!string.IsNullOrEmpty(PartitionKey))
        {
            header.PartitionKey = PartitionKey!;
        }

        if (!string.IsNullOrEmpty(ContentType))
        {
            header.ContentType = ContentType!;
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

        var body = new MessageBody(Body, BodyContentType, characterEncoding);

        return new Message(header, body);
    }
}
