using MongoDB.Bson;
using MongoDB.Driver;

namespace Paramore.Brighter.Inbox.MongoDb;

/// <summary>
/// The inbox implementation to MongoDB 
/// </summary>
public class MongoDbInbox : IAmAnInboxAsync, IAmAnInboxSync
{
    private IMongoCollection<InboxMessage>? _collection;
    private readonly MongoClient _client;
    private readonly IMongoDatabase _database;
    private readonly TimeProvider _timeProvider;
    private readonly MongoDbInboxConfiguration _configuration;

    /// <summary>
    /// Initialize a new instance of <see cref="MongoDbInbox"/>.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    public MongoDbInbox(MongoDbInboxConfiguration configuration)
    {
        _client = configuration.Client;
        _database = _client.GetDatabase(configuration.DatabaseName, configuration.DatabaseSettings);
        _configuration = configuration;
        _timeProvider = configuration.TimeProvider;
    }

    /// <inheritdoc />
    public bool ContinueOnCapturedContext { get; set; }

    /// <inheritdoc />
    public async Task AddAsync<T>(T command, string contextKey, int timeoutInMilliseconds = -1,
        CancellationToken cancellationToken = default) where T : class, IRequest
    {
        var message = new InboxMessage(command, contextKey, _timeProvider.GetUtcNow());

        var collection = await GetCollectionAsync(cancellationToken);

        await collection.InsertOneAsync(message, cancellationToken: cancellationToken)
            .ConfigureAwait(ContinueOnCapturedContext);
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string id, string contextKey, int timeoutInMilliseconds = -1,
        CancellationToken cancellationToken = default) where T : class, IRequest
    {
        var commandId = new InboxMessage.InboxMessageId { Id = id, ContextKey = contextKey };
        var filter = Builders<InboxMessage>.Filter.Eq("Id", commandId);

        var collection = await GetCollectionAsync(cancellationToken);
        var command = await collection.Find(filter)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(ContinueOnCapturedContext);
        return command.ToCommand<T>();
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync<T>(string id, string contextKey, int timeoutInMilliseconds = -1,
        CancellationToken cancellationToken = default) where T : class, IRequest
    {
        var commandId = new InboxMessage.InboxMessageId { Id = id, ContextKey = contextKey };
        var filter = Builders<InboxMessage>.Filter.Eq("Id", commandId);
        var collection = await GetCollectionAsync(cancellationToken);
        return await collection.Find(filter)
            .AnyAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(ContinueOnCapturedContext);
    }


    private ValueTask<IMongoCollection<InboxMessage>> GetCollectionAsync(CancellationToken cancellationToken = default)
    {
        if (_collection != null)
        {
            return new ValueTask<IMongoCollection<InboxMessage>>(_collection);
        }

        if (_configuration.MakeCollection == OnResolvingAInboxCollection.Assume)
        {
            _collection =
                _database.GetCollection<InboxMessage>(_configuration.CollectionName,
                    _configuration.CollectionSettings);
            return new ValueTask<IMongoCollection<InboxMessage>>(_collection);
        }

        return new ValueTask<IMongoCollection<InboxMessage>>(GetOrCreateAsync());

        async Task<IMongoCollection<InboxMessage>> GetOrCreateAsync()
        {
            var filter = new BsonDocument("name", _configuration.CollectionName);
            var options = new ListCollectionNamesOptions { Filter = filter };

            var collections = await _database.ListCollectionNamesAsync(options, cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

            if (await collections.AnyAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext))
            {
                _collection = _database.GetCollection<InboxMessage>(_configuration.CollectionName,
                    _configuration.CollectionSettings);
                return _collection;
            }

            if (_configuration.MakeCollection == OnResolvingAInboxCollection.Validate)
            {
                throw new InvalidOperationException("collection not exits");
            }

            using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

            await _database
                .CreateCollectionAsync(session, _configuration.CollectionName, _configuration.CreateCollectionOptions,
                    cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

            _collection =
                _database.GetCollection<InboxMessage>(_configuration.CollectionName,
                    _configuration.CollectionSettings);
            return _collection;
        }
    }

    /// <inheritdoc />
    public void Add<T>(T command, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
    {
        var message = new InboxMessage(command, contextKey, _timeProvider.GetUtcNow());

        var collection = GetCollection();

        collection.InsertOne(message);
    }

    /// <inheritdoc />
    public T Get<T>(string id, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
    {
        var commandId = new InboxMessage.InboxMessageId { Id = id, ContextKey = contextKey };
        var filter = Builders<InboxMessage>.Filter.Eq("Id", commandId);

        var collection = GetCollection();
        var command = collection.Find(filter).First();
        return command.ToCommand<T>();
    }

    /// <inheritdoc />
    public bool Exists<T>(string id, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
    {
        var commandId = new InboxMessage.InboxMessageId { Id = id, ContextKey = contextKey };
        var filter = Builders<InboxMessage>.Filter.Eq("Id", commandId);
        var collection = GetCollection();
        return collection.Find(filter)
            .Any();
    }

    private IMongoCollection<InboxMessage> GetCollection()
    {
        if (_collection != null)
        {
            return _collection;
        }

        if (_configuration.MakeCollection == OnResolvingAInboxCollection.Assume)
        {
            _collection =
                _database.GetCollection<InboxMessage>(_configuration.CollectionName,
                    _configuration.CollectionSettings);
            return _collection;
        }

        var filter = new BsonDocument("name", _configuration.CollectionName);
        var options = new ListCollectionNamesOptions { Filter = filter };

        var collections = _database.ListCollectionNames(options);
        if (collections.Any())
        {
            _collection = _database.GetCollection<InboxMessage>(_configuration.CollectionName,
                _configuration.CollectionSettings);
            return _collection;
        }

        if (_configuration.MakeCollection == OnResolvingAInboxCollection.Validate)
        {
            throw new InvalidOperationException("collection not exits");
        }

        using var session = _client.StartSession();

        _database
            .CreateCollection(session, _configuration.CollectionName, _configuration.CreateCollectionOptions);

        _collection =
            _database.GetCollection<InboxMessage>(_configuration.CollectionName,
                _configuration.CollectionSettings);
        return _collection;
    }
}
