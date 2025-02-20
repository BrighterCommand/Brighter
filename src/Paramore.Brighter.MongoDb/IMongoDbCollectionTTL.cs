namespace Paramore.Brighter.MongoDb;

/// <summary>
/// The MongoDB collection TTL
/// </summary>
public interface IMongoDbCollectionTTL
{
    /// <summary>
    /// The timestamp of when the message was created
    /// </summary>
    DateTimeOffset TimeStamp { get; set; }
    
    /// <summary>
    /// For how long a doc should live
    /// </summary>
    long? ExpireAfterSeconds { get; set; }
}
