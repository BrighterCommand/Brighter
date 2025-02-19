namespace Paramore.Brighter.MongoDb;

/// <summary>
/// The MongoDB collection TTL
/// </summary>
public interface IMongoDbCollectionTTL
{
    /// <summary>
    /// For how long a doc should live
    /// </summary>
    long? ExpireAfterSeconds { get; set; }
}
