using MongoDB.Bson;
using MongoDB.Driver;

namespace Paramore.Brighter.MongoDb;


/// <summary>
/// Provides a base class for MongoDB data access, encapsulating common operations
/// for interacting with a MongoDB collection that supports Time-To-Live (TTL) functionality.
/// It handles collection resolution, creation (if configured), and TTL index management.
/// </summary>
/// <typeparam name="TCollection">
/// The type of the document stored in the MongoDB collection, which must implement
/// <see cref="IMongoDbCollectionTTL"/> to support TTL expiration.
/// </typeparam>
public abstract class BaseMongoDb<TCollection>
    where TCollection : IMongoDbCollectionTTL
{
    private readonly IMongoClient _client;
    private readonly IMongoDatabase _database;
    private IMongoCollection<TCollection>? _collection;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseMongoDb{TCollection}"/> class.
    /// </summary>
    /// <param name="connectionProvider">The MongoDB connection provider, supplying the <see cref="IMongoClient"/>.</param>
    /// <param name="configuration">The overall MongoDB configuration for Brighter.</param>
    /// <param name="collectionConfiguration">The specific configuration for this MongoDB collection.</param>
    protected BaseMongoDb(IAmAMongoDbConnectionProvider connectionProvider, IAmAMongoDbConfiguration configuration, MongoDbCollectionConfiguration collectionConfiguration)
    {
        _client = connectionProvider.Client;
        CollectionConfiguration = collectionConfiguration;
        _database = _client.GetDatabase(configuration.DatabaseName, configuration.DatabaseSettings);
        Configuration = configuration;
    }
    
    /// <summary>
    /// Gets the overall MongoDB configuration for Brighter.
    /// </summary>
    protected IAmAMongoDbConfiguration Configuration { get; }

    /// <summary>
    /// Gets the specific configuration for this MongoDB collection, including its name,
    /// resolution strategy, and other options.
    /// </summary>
    protected MongoDbCollectionConfiguration CollectionConfiguration { get; }

    /// <summary>
    /// Gets the calculated Time-To-Live (TTL) duration in seconds for documents in this collection.
    /// This value is derived from the <see cref="MongoDbCollectionConfiguration.TimeToLive"/> property.
    /// Returns null if no TTL is configured.
    /// </summary>
    protected long? ExpireAfterSeconds
    {
        get
        {
            if (CollectionConfiguration.TimeToLive.HasValue)
            {
                return (long)CollectionConfiguration.TimeToLive.Value.TotalSeconds;
            }

            return null;
        }
    }

   
    /// <summary>
    /// Gets the <see cref="IMongoCollection{TCollection}"/> instance for the configured collection.
    /// This property ensures the collection is resolved or created according to the
    /// <see cref="MongoDbCollectionConfiguration.MakeCollection"/> setting before returning.
    /// A configured TTL index is ensured before the collection is cached. Index creation
    /// errors propagate to the caller, and subsequent access retries initialization.
    /// </summary>
    protected IMongoCollection<TCollection> Collection => _collection ??= CreateCollection();

    private const int NamespaceExists = 48;
    
    private IMongoCollection<TCollection> CreateCollection()
    {
        if (CollectionConfiguration.MakeCollection != OnResolvingACollection.Assume)
        {
            EnsureCollectionExists();
        }

        var collection = _database.GetCollection<TCollection>(CollectionConfiguration.Name, CollectionConfiguration.Settings);
        if (CollectionConfiguration.TimeToLive != null)
        {
            collection.Indexes.CreateOne(
                new CreateIndexModel<TCollection>(Builders<TCollection>.IndexKeys.Ascending(x => x.TimeStamp),
                    new CreateIndexOptions
                    {
                        Name = $"brighter_ttl_{CollectionConfiguration.Name}",
                        ExpireAfter = CollectionConfiguration.TimeToLive
                    }));
        }

        return collection;
    }

    private void EnsureCollectionExists()
    {
        var filter = new BsonDocument("name", CollectionConfiguration.Name);
        var options = new ListCollectionNamesOptions { Filter = filter };

        using var collections = _database.ListCollectionNames(options);
        if (collections.Any())
        {
            return;
        }

        if (CollectionConfiguration.MakeCollection == OnResolvingACollection.Validate)
        {
            throw new InvalidOperationException("collection not exits");
        }
        try
        {
            using var session = _client.StartSession();
            _database.CreateCollection(session, CollectionConfiguration.Name, CollectionConfiguration.CreateCollectionOptions);
        }
        catch(MongoCommandException ex) when(ex.Code == NamespaceExists)
        {
        }
    }
}
