using MongoDB.Driver;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.MongoDb;

/// <summary>
/// Implements the <see cref="IAmAMongoDbConfiguration"/> interface, providing a concrete
/// configuration for Brighter's MongoDB integration. This class facilitates setting up
/// the MongoDB client, database name, and specific collection configurations for
/// outbox, inbox, and locking mechanisms.
/// </summary>
public class MongoDbConfiguration : IAmAMongoDbConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDbConfiguration"/> class with a pre-configured
    /// MongoDB client and the database name.
    /// </summary>
    /// <param name="client">The <see cref="IMongoClient"/> instance to use for MongoDB operations.</param>
    /// <param name="databaseName">The name of the MongoDB database.</param>
    public MongoDbConfiguration(IMongoClient client, string databaseName)
    {
        Client = client;
        DatabaseName = databaseName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDbConfiguration"/> class using a connection string
    /// and the database name. A new <see cref="MongoClient"/> is created internally.
    /// </summary>
    /// <param name="connectionString">The MongoDB connection string.</param>
    /// <param name="databaseName">The name of the MongoDB database.</param>
    public MongoDbConfiguration(string connectionString, string databaseName)
        : this(new MongoClient(connectionString), databaseName)
    {
    }

    /// <inheritdoc />
    public IMongoClient Client { get; set; }

    /// <inheritdoc />
    public string DatabaseName { get; }

    /// <inheritdoc />
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
    
    /// <inheritdoc />
    public MongoDatabaseSettings? DatabaseSettings { get; set; }

    /// <inheritdoc />
    public InstrumentationOptions InstrumentationOptions { get; set; } = InstrumentationOptions.All;
    
    /// <inheritdoc />
    public MongoDbCollectionConfiguration? Outbox { get; set; }
    
    /// <inheritdoc />
    public MongoDbCollectionConfiguration? Inbox { get; set; }

    /// <inheritdoc />
    public MongoDbCollectionConfiguration? Locking { get; set; }
}
