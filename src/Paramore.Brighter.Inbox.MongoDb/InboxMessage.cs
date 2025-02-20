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
    }

    /// <summary>
    /// Initialize new instance of <see cref="InboxMessage"/>
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="id">The command id.</param>
    /// <param name="contextKey">The context key.</param>
    /// <param name="timeStamp">The time stamp of when the message was created.</param>
    /// <param name="expireAfterSeconds">The expires after X seconds.</param>
    public InboxMessage(object command, string id, string contextKey, DateTimeOffset timeStamp, long? expireAfterSeconds)
    {
        Id = new InboxMessageId { Id = id, ContextKey = contextKey };
        TimeStamp = timeStamp;
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
    /// The <see cref="DateTimeOffset"/> when the message was crated
    /// </summary>
    public DateTimeOffset TimeStamp { get; set; }

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
