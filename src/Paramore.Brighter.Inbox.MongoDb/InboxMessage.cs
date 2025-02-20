using System.Text.Json;
using MongoDB.Bson.Serialization.Attributes;
using Paramore.Brighter.MongoDb;

namespace Paramore.Brighter.Inbox.MongoDb;

/// <summary>
/// The MongoDb inbox message
/// </summary>
public class InboxMessage : IMongoDbCollectionTTL
{
    /// <summary>
    /// Initialize new instance of <see cref="InboxMessage"/>
    /// </summary>
    public InboxMessage()
    {
        var timeStamp = DateTimeOffset.UtcNow;
        CreatedTime = timeStamp.Ticks;
        CreatedAt = timeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    }

    /// <summary>
    /// Initialize new instance of <see cref="InboxMessage"/>
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="contextKey">The context key.</param>
    /// <param name="timeStamp">The time stamp of when the message was created.</param>
    /// <param name="expireAfterSeconds">The expires after X seconds.</param>
    public InboxMessage(object command, string id, string contextKey, DateTimeOffset timeStamp, long? expireAfterSeconds)
    {
        Id = new InboxMessageId { Id = id, ContextKey = contextKey };
        CreatedTime = timeStamp.Ticks;
        CreatedAt = timeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        CommandType = command.GetType().FullName!;
        CommandBody = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);
        ExpireAfterSeconds = expireAfterSeconds;
    }

    /// <summary>
    /// The Message ID
    /// </summary>
    [BsonId]
    public InboxMessageId Id { get; set; } = new();

    /// <summary>
    /// The time at which the message was created, in ticks
    /// </summary>
    public long CreatedTime { get; set; }

    /// <summary>
    /// The time at which the message was created, formatted as a string yyyy-MM-ddTHH:mm:ss.fffZ
    /// </summary>
    public string CreatedAt { get; set; }

    /// <summary>
    /// The command type(the full name)
    /// </summary>
    public string CommandType { get; set; } = string.Empty;

    /// <summary>
    /// The command body
    /// </summary>
    public string CommandBody { get; set; } = string.Empty;

    /// <summary>
    /// The TTL for this message
    /// </summary>
    public long? ExpireAfterSeconds { get; set; }

    /// <summary>
    /// The inbox message id
    /// </summary>
    public class InboxMessageId
    {
        /// <summary>
        /// The id.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// The context key.
        /// </summary>
        public string? ContextKey { get; set; }
    }

    /// <summary>
    /// Convert the <see cref="CommandBody"/> to <typeparamref name="T"/>
    /// </summary>
    /// <typeparam name="T">The <see cref="IRequest"/>.</typeparam>
    /// <returns>New instance of <typeparamref cref="T"/>.</returns>
    public T ToCommand<T>()
        => JsonSerializer.Deserialize<T>(CommandBody, JsonSerialisationOptions.Options)!;
}
