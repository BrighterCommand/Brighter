using MongoDB.Driver;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.MongoDb;

/// <summary>
/// Represents the configuration for Brighter's MongoDB integration.
/// This interface defines the necessary properties for connecting to and interacting with a MongoDB database
/// within a Brighter application, including specific collections for outbox, inbox, and locking mechanisms.
/// </summary>`
public interface IAmAMongoDbConfiguration
{
    /// <summary>
    /// Gets or sets the MongoDB client instance.
    /// This client is the entry point for all MongoDB operations and manages connections to the database.
    /// </summary>
    IMongoClient Client { get; set; }

    /// <summary>
    /// The mongodb database name
    /// </summary>
    string DatabaseName { get; }

    /// <summary>
    /// Gets or sets the <see cref="System.TimeProvider"/> instance.
    /// This is used for providing time-related functionality within the MongoDB integration,
    /// particularly for scenarios requiring precise time-based operations or testing.
    /// </summary>
    TimeProvider TimeProvider { get; set; } 
    
    /// <summary>
    /// Gets or sets the <see cref="MongoDatabaseSettings"/> used when accessing the database.
    /// These settings can include configurations such as read preference, write concern, and other database-specific options.
    /// </summary>
    MongoDatabaseSettings? DatabaseSettings { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="InstrumentationOptions"/>.
    /// These options allow for configuration of monitoring and tracing capabilities for MongoDB operations,
    /// providing insights into database interactions.
    /// </summary> 
    InstrumentationOptions InstrumentationOptions { get; set; } 
    
    /// <summary>
    /// Gets or sets the configuration for the outbox collection.
    /// The outbox pattern is used in message-driven architectures to ensure transactional consistency
    /// between database operations and message publishing.
    /// </summary>
    MongoDbCollectionConfiguration? Outbox { get; set; }
    
    /// <summary>
    /// Gets or sets the configuration for the inbox collection.
    /// The inbox pattern is used to ensure idempotent message processing, preventing duplicate processing
    /// of messages received by the service.
    /// </summary>
    MongoDbCollectionConfiguration? Inbox { get; set; }
    
    /// <summary>
    /// Gets or sets the configuration for the locking collection.
    /// This collection is typically used for implementing distributed locks to coordinate access
    /// to shared resources or to ensure singular execution of specific operations across multiple instances.
    /// </summary>
    MongoDbCollectionConfiguration? Locking { get; set; }
}
