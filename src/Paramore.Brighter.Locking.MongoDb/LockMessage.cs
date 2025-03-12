using MongoDB.Bson.Serialization.Attributes;
using Paramore.Brighter.MongoDb;

namespace Paramore.Brighter.Locking.MongoDb;

/// <summary>
/// The lock message
/// </summary>
public class LockMessage : IMongoDbCollectionTTL
{
    /// <summary>
    /// The Lock id
    /// </summary>
    [BsonId] public string Id { get; set; } = string.Empty;

    /// <inheritdoc />
    public DateTimeOffset TimeStamp { get; set; }

    /// <inheritdoc />
    public long? ExpireAfterSeconds { get; set; }
}
