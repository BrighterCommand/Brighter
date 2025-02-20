using MongoDB.Bson;
using MongoDB.Driver;

namespace Paramore.Brighter.MongoDb;

/// <summary>
/// The base class for any class that need to access mongodb.
/// </summary>
/// <typeparam name="TCollection">The Collection type</typeparam>
public abstract class BaseMongoDb<TCollection>
    where TCollection : IMongoDbCollectionTTL
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly MongoClient _client;
    private readonly IMongoDatabase _database;
    private IMongoCollection<TCollection>? _collection;

    /// <summary>
    /// Initializer the <see cref="BaseMongoDb{TCollection}"/>
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    protected BaseMongoDb(MongoDbConfiguration configuration)
    {
        _client = configuration.Client;
        _database = _client.GetDatabase(configuration.DatabaseName, configuration.DatabaseSettings);
        Configuration = configuration;
    }

    /// <summary>
    /// The <see cref="MongoDbConfiguration"/>.
    /// </summary>
    protected MongoDbConfiguration Configuration { get; }

    /// <summary>
    /// The provided TTL in seconds
    /// </summary>
    protected long? ExpireAfterSeconds
    {
        get
        {
            if (Configuration.TimeToLive.HasValue)
            {
                return (long)Configuration.TimeToLive.Value.TotalSeconds;
            }

            return null;
        }
    }

    /// <summary>
    /// The <see cref="IMongoCollection{TDocument}"/>
    /// </summary>
    protected IMongoCollection<TCollection> Collection => _collection ??= CreateCollection();

    /// <summary>
    /// Get or create a collection.
    /// </summary>
    /// <returns>The <see cref="IMongoCollection{TDocument}"/></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private IMongoCollection<TCollection> CreateCollection()
    {
        _semaphore.Wait();
        try
        {
            if (_collection != null)
            {
                return _collection;
            }

            if (Configuration.MakeCollection == OnResolvingACollection.Assume)
            {
                _collection =
                    _database.GetCollection<TCollection>(Configuration.CollectionName,
                        Configuration.CollectionSettings);
                return _collection;
            }

            var filter = new BsonDocument("name", Configuration.CollectionName);
            var options = new ListCollectionNamesOptions { Filter = filter };

            var collections = _database.ListCollectionNames(options);
            if (collections.Any())
            {
                _collection =
                    _database.GetCollection<TCollection>(Configuration.CollectionName,
                        Configuration.CollectionSettings);
                return _collection;
            }

            if (Configuration.MakeCollection == OnResolvingACollection.Validate)
            {
                throw new InvalidOperationException("collection not exits");
            }

            using var session = _client.StartSession();

            _database
                .CreateCollection(session, Configuration.CollectionName, Configuration.CreateCollectionOptions);

            _collection =
                _database.GetCollection<TCollection>(Configuration.CollectionName, Configuration.CollectionSettings);

            if (Configuration.TimeToLive != null)
            {
                _collection.Indexes.CreateOne(
                    new CreateIndexModel<TCollection>(Builders<TCollection>.IndexKeys.Ascending(x => x.TimeStamp),
                        new CreateIndexOptions
                        {
                            Name = $"brighter_ttl_{Configuration.CollectionName}",
                            ExpireAfter = Configuration.TimeToLive
                        }));
            }

            return _collection;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
