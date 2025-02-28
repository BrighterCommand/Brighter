using MongoDB.Driver;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.MongoDb;

/// <summary>
/// The MongoDB configuration
/// </summary>
public class MongoDbConfiguration
{
    /// <summary>
    /// Initialize new instance of <see cref="MongoDbConfiguration"/>
    /// </summary>
    /// <param name="client">The Mongo client.</param>
    /// <param name="databaseName">The database name.</param>
    /// <param name="collectionName">The collection name.</param>
    public MongoDbConfiguration(MongoClient client, string databaseName, string collectionName)
    {
        Client = client;
        DatabaseName = databaseName;
        CollectionName = collectionName;
    }

    /// <summary>
    /// Initialize new instance of <see cref="MongoDbConfiguration"/>
    /// </summary>
    /// <param name="connectionString">The Mongo db connection string.</param>
    /// <param name="databaseName">The database name.</param>
    /// <param name="collectionName">The collection name.</param>
    public MongoDbConfiguration(string connectionString, string databaseName, string collectionName)
        : this(new MongoClient(connectionString), databaseName, collectionName)
    {
    }

    /// <summary>
    /// The <see cref="MongoClient"/>
    /// </summary>
    public MongoClient Client { get; set; }

    /// <summary>
    /// The mongodb database name
    /// </summary>
    public string DatabaseName { get; }

    /// <summary>
    /// The mongodb collection
    /// </summary>
    public string CollectionName { get; }

    /// <summary>
    /// The <see cref="System.TimeProvider"/>
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>
    /// Action to be performed when it's resolving a collection  
    /// </summary>
    public OnResolvingACollection MakeCollection { get; set; } = OnResolvingACollection.Assume;

    /// <summary>
    /// The <see cref="MongoDatabaseSettings"/> used when access the database.
    /// </summary>
    public MongoDatabaseSettings? DatabaseSettings { get; set; }

    /// <summary>
    /// The <see cref="MongoDatabaseSettings"/> used to get collection
    /// </summary>
    public MongoCollectionSettings? CollectionSettings { get; set; }

    /// <summary>
    /// The <see cref="CreateCollectionOptions"/>.
    /// </summary>
    public CreateCollectionOptions? CreateCollectionOptions { get; set; }

    /// <summary>
    /// The <see cref="InstrumentationOptions"/>.
    /// </summary>
    public InstrumentationOptions InstrumentationOptions { get; set; } = InstrumentationOptions.All;

    /// <summary>
    /// Optional time to live for the messages in the outbox
    /// By default, messages will not expire
    /// </summary>
    public TimeSpan? TimeToLive { get; set; }
}
