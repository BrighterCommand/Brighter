namespace Paramore.Brighter.MongoDb;

/// <summary>
/// Represents a contract for a MongoDB document that includes properties for
/// managing its Time-To-Live (TTL) expiration. This interface is intended
/// for documents that will reside in a MongoDB collection configured with a TTL index.
/// </summary>
public interface IMongoDbCollectionTTL
{
    /// <summary>
    /// Gets or sets the timestamp used for TTL expiration.
    /// This property typically corresponds to a date field in the MongoDB document
    /// against which the TTL index will be created. The document will expire
    /// a certain duration after this timestamp.
    /// </summary>
    DateTimeOffset TimeStamp { get; set; }
    
    /// <summary>
    /// Gets or sets the duration in seconds after the <see cref="TimeStamp"/>
    /// that the document should expire. A null value indicates that no specific
    /// override for expiration is set at the document level, and the collection's
    /// default TTL (if any) would apply.
    /// </summary>
    long? ExpireAfterSeconds { get; set; }
}
