using MongoDB.Bson;
using MongoDB.Driver;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Outbox.MongoDb;

public class MongoDbOutbox : IAmAnOutboxAsync<Message, IClientSessionHandle>
{
    private IMongoCollection<MessageItem>? _collection;
    private readonly MongoClient _client;
    private readonly IMongoDatabase _database;
    private readonly TimeProvider _timeProvider;
    private readonly MongoDbConfiguration _configuration;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="client"></param>
    /// <param name="configuration"></param>
    /// <param name="provider"></param>
    public MongoDbOutbox(MongoClient client, MongoDbConfiguration configuration, TimeProvider provider)
    {
        _client = client;
        _database = client.GetDatabase(configuration.DatabaseName, configuration.DatabaseSettings);
        _configuration = configuration;
        _timeProvider = provider;
    }

    /// <inheritdoc />
    public IAmABrighterTracer? Tracer { get; set; }

    /// <inheritdoc />
    public bool ContinueOnCapturedContext { get; set; }

    /// <inheritdoc />
    public async Task AddAsync(Message message,
        RequestContext requestContext,
        int outBoxTimeout = -1,
        IAmABoxTransactionProvider<IClientSessionHandle>? transactionProvider = null,
        CancellationToken cancellationToken = default)
    {
        var expiresAt = GetExpirationTime();
        var messageToStore = new MessageItem(message, expiresAt);
        var collection = await GetCollectionAsync(cancellationToken);

        if (transactionProvider != null)
        {
            var session = await transactionProvider.GetTransactionAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

            await collection
                .InsertOneAsync(session, messageToStore, cancellationToken: cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }
        else
        {
            await collection
                .InsertOneAsync(messageToStore, cancellationToken: cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }
    }

    /// <inheritdoc />
    public async Task AddAsync(IEnumerable<Message> messages,
        RequestContext? requestContext,
        int outBoxTimeout = -1,
        IAmABoxTransactionProvider<IClientSessionHandle>? transactionProvider = null,
        CancellationToken cancellationToken = default)
    {
        var expiresAt = GetExpirationTime();
        var messageItems = messages.Select(message => new MessageItem(message, expiresAt));
        var collection = await GetCollectionAsync(cancellationToken);
        if (transactionProvider != null)
        {
            var session = await transactionProvider.GetTransactionAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
            await collection
                .InsertManyAsync(session, messageItems, cancellationToken: cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }
        else
        {
            await collection
                .InsertManyAsync(messageItems, cancellationToken: cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string[] messageIds,
        RequestContext requestContext,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetCollectionAsync(cancellationToken);
        var filter = Builders<MessageItem>.Filter.In(x => x.MessageId, messageIds);
        await collection.DeleteManyAsync(filter, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
    }

    public Task<IEnumerable<Message>> DispatchedMessagesAsync(TimeSpan dispatchedSince, RequestContext requestContext,
        int pageSize = 100,
        int pageNumber = 1, int outboxTimeout = -1, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async Task<Message> GetAsync(string messageId, RequestContext requestContext, int outBoxTimeout = -1,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        return await GetMessageAsync(messageId, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
    }

    /// <inheritdoc />
    public async Task MarkDispatchedAsync(string id, RequestContext requestContext, DateTimeOffset? dispatchedAt = null,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
    {
        var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        var filter = Builders<MessageItem>.Filter.Eq(x => x.MessageId, id);

        dispatchedAt ??= _timeProvider.GetUtcNow();
        var update = Builders<MessageItem>.Update
            .Set(x => x.DeliveryTime, dispatchedAt.Value.Ticks)
            .Set(x => x.DeliveredAt, dispatchedAt)
            .Unset(x => x.OutstandingCreatedTime);
        
        await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
            .ConfigureAwait(ContinueOnCapturedContext);
    }

    /// <inheritdoc />
    public async Task MarkDispatchedAsync(IEnumerable<string> ids, 
        RequestContext requestContext,
        DateTimeOffset? dispatchedAt = null,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
    {
         var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        var filter = Builders<MessageItem>.Filter.In(x => x.MessageId, ids);

        dispatchedAt ??= _timeProvider.GetUtcNow();
        var update = Builders<MessageItem>.Update
            .Set(x => x.DeliveryTime, dispatchedAt.Value.Ticks)
            .Set(x => x.DeliveredAt, dispatchedAt)
            .Unset(x => x.OutstandingCreatedTime);
        
        await collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken)
            .ConfigureAwait(ContinueOnCapturedContext);
    }

    public Task<IEnumerable<Message>> OutstandingMessagesAsync(TimeSpan dispatchedSince, RequestContext requestContext,
        int pageSize = 100,
        int pageNumber = 1, Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }


    private async Task<Message> GetMessageAsync(string id, CancellationToken cancellationToken = default)
    {
        var collection = await GetCollectionAsync(cancellationToken);
        var find = await collection
            .FindAsync(x => x.MessageId == id, cancellationToken: cancellationToken)
            .ConfigureAwait(ContinueOnCapturedContext);

        var first = await find
            .FirstOrDefaultAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(ContinueOnCapturedContext);

        return first == null ? new Message() : first.ConvertToMessage();
    }

    private long? GetExpirationTime()
    {
        if (_configuration.TimeToLive.HasValue)
        {
            return _timeProvider.GetUtcNow().Add(_configuration.TimeToLive.Value).ToUnixTimeSeconds();
        }

        return null;
    }

    private async Task<IClientSession> GetSessionAsync(IAmABoxTransactionProvider<IClientSession>? provider,
        CancellationToken cancellationToken = default)
    {
        if (provider != null)
        {
            return await provider.GetTransactionAsync(cancellationToken);
        }

        return await _client.StartSessionAsync(cancellationToken: cancellationToken);
    }

    private ValueTask<IMongoCollection<MessageItem>> GetCollectionAsync(CancellationToken cancellationToken = default)
    {
        if (_collection != null)
        {
            return new ValueTask<IMongoCollection<MessageItem>>(_collection);
        }

        if (_configuration.OnResolvingACollection == OnResolvingACollection.Assume)
        {
            _collection =
                _database.GetCollection<MessageItem>(_configuration.CollectionName, _configuration.CollectionSettings);
            return new ValueTask<IMongoCollection<MessageItem>>(_collection);
        }

        return new ValueTask<IMongoCollection<MessageItem>>(GetOrCreateAsync());

        async Task<IMongoCollection<MessageItem>> GetOrCreateAsync()
        {
            var filter = new BsonDocument("name", _configuration.CollectionName);
            var options = new ListCollectionNamesOptions { Filter = filter };

            var collections = await _database.ListCollectionNamesAsync(options, cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

            if (await collections.AnyAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext))
            {
                _collection = _database.GetCollection<MessageItem>(_configuration.CollectionName,
                    _configuration.CollectionSettings);
                return _collection;
            }

            if (_configuration.OnResolvingACollection == OnResolvingACollection.Validate)
            {
                throw new InvalidOperationException("collection not exits");
            }

            using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

            await _database
                .CreateCollectionAsync(session, _configuration.CollectionName, _configuration.CreateCollectionOptions,
                    cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

            _collection =
                _database.GetCollection<MessageItem>(_configuration.CollectionName, _configuration.CollectionSettings);
            return _collection;
        }
    }
}
