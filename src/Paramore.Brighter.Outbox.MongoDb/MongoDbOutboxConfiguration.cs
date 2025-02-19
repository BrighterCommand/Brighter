using MongoDB.Driver;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Outbox.MongoDb;

/// <summary>
/// The MongoDB configuration
/// </summary>
public class MongoDbOutboxConfiguration
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="connectionString"></param>
    /// <param name="databaseName"></param>
    /// <param name="collectionName"></param>
    public MongoDbOutboxConfiguration(string connectionString, string databaseName, string? collectionName = null)
    {
        ConnectionString = connectionString;
        DatabaseName = databaseName;
        CollectionName = collectionName ?? "brighter_outbox";
        Client = new MongoClient(connectionString);
    }
    
    
    /// <summary>
    /// The <see cref="MongoClient"/>
    /// </summary>
    public MongoClient Client { get; set; }

    /// <summary>
    /// The mongo db connection string
    /// </summary>
    public string ConnectionString { get; }

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
    /// Timeout in milliseconds
    /// </summary>
    public int Timeout { get; set; } = 500;


    /// <summary>
    /// Action to be performed when it's resolving a collection  
    /// </summary>
    public OnResolvingAOutboxCollection MakeCollection { get; set; } = OnResolvingAOutboxCollection.Assume;

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
    public TimeSpan? TimeToLive
    {
        get => CreateCollectionOptions?.ExpireAfter;
        set
        {
            CreateCollectionOptions ??= new CreateCollectionOptions();
            CreateCollectionOptions.ExpireAfter = value;
        }
    }
}
