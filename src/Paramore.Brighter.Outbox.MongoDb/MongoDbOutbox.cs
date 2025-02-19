using MongoDB.Bson;
using MongoDB.Driver;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Outbox.MongoDb;

/// <summary>
/// The implemention for MongoDB for outbox
/// </summary>
public class MongoDbOutbox : IAmAnOutboxAsync<Message, IClientSessionHandle>,
    IAmAnOutboxSync<Message, IClientSessionHandle>
{
    private IMongoCollection<OutboxMessage>? _collection;
    private readonly MongoClient _client;
    private readonly IMongoDatabase _database;
    private readonly TimeProvider _timeProvider;
    private readonly MongoDbConfiguration _configuration;

    /// <summary>
    /// Initialize MongoDbOutbox
    /// </summary>
    /// <param name="client">The <see cref="MongoClient"/>.</param>
    /// <param name="configuration">The <see cref="MongoDbConfiguration"/>.</param>
    /// <param name="provider">The <see cref="System.TimeProvider"/></param>
    public MongoDbOutbox(MongoClient client, MongoDbConfiguration configuration, TimeProvider? provider = null)
    {
        _client = client;
        _database = client.GetDatabase(configuration.DatabaseName, configuration.DatabaseSettings);
        _configuration = configuration;
        _timeProvider = provider ?? TimeProvider.System;
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
        var span = Tracer?.CreateDbSpan(
            new OutboxSpanInfo(DbSystem.Mongodb,
                _database.DatabaseNamespace.DatabaseName,
                OutboxDbOperation.Add,
                _configuration.CollectionName),
            requestContext?.Span,
            options: _configuration.InstrumentationOptions);

        try
        {
            var expiresAt = GetExpirationTime();
            var messageToStore = new OutboxMessage(message, expiresAt);
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
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task AddAsync(IEnumerable<Message> messages,
        RequestContext? requestContext,
        int outBoxTimeout = -1,
        IAmABoxTransactionProvider<IClientSessionHandle>? transactionProvider = null,
        CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateDbSpan(
            new OutboxSpanInfo(DbSystem.Mongodb,
                _database.DatabaseNamespace.DatabaseName,
                OutboxDbOperation.Add,
                _configuration.CollectionName),
            requestContext?.Span,
            options: _configuration.InstrumentationOptions);
        try
        {
            var expiresAt = GetExpirationTime();
            var messageItems = messages.Select(message => new OutboxMessage(message, expiresAt));
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
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string[] messageIds,
        RequestContext requestContext,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateDbSpan(
            new OutboxSpanInfo(DbSystem.Mongodb,
                _database.DatabaseNamespace.DatabaseName,
                OutboxDbOperation.Delete,
                _configuration.CollectionName),
            requestContext?.Span,
            options: _configuration.InstrumentationOptions);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken);
            var filter = Builders<OutboxMessage>.Filter.In(x => x.MessageId, messageIds);
            await collection.DeleteManyAsync(filter, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Message>> DispatchedMessagesAsync(TimeSpan dispatchedSince,
        RequestContext requestContext,
        int pageSize = 100,
        int pageNumber = 1, int outboxTimeout = -1, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateDbSpan(
            new OutboxSpanInfo(DbSystem.Mongodb,
                _database.DatabaseNamespace.DatabaseName,
                OutboxDbOperation.DispatchedMessages,
                _configuration.CollectionName),
            requestContext?.Span,
            options: _configuration.InstrumentationOptions);
        try
        {
            var olderThan = _timeProvider.GetLocalNow() - dispatchedSince;
            var filter = Builders<OutboxMessage>.Filter.Lt(x => x.DeliveryTime, olderThan.Ticks);
            if (args != null && args.TryGetValue("Topic", out var topic))
            {
                filter &= Builders<OutboxMessage>.Filter.Eq(x => x.Topic, topic);
            }

            var collection = await GetCollectionAsync(cancellationToken);
            var cursor = await collection.FindAsync(filter,
                    new FindOptions<OutboxMessage, OutboxMessage>
                    {
                        Limit = pageSize,
                        Skip = pageSize * Math.Max(pageNumber - 1, 0),
                        Sort = Builders<OutboxMessage>.Sort.Ascending(x => x.CreatedTime)
                    }, cancellationToken: cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

            var messages = new List<Message>(pageSize);
            while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
            {
                messages.AddRange(cursor.Current.Select(x => x.ConvertToMessage()));
            }

            return messages;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task<Message> GetAsync(string messageId, RequestContext requestContext, int outBoxTimeout = -1,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateDbSpan(
            new OutboxSpanInfo(DbSystem.Mongodb,
                _database.DatabaseNamespace.DatabaseName,
                OutboxDbOperation.Get,
                _configuration.CollectionName),
            requestContext?.Span,
            options: _configuration.InstrumentationOptions);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken);
            var find = await collection
                .FindAsync(x => x.MessageId == messageId, cancellationToken: cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

            var first = await find
                .FirstOrDefaultAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

            return first == null ? new Message() : first.ConvertToMessage();
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task MarkDispatchedAsync(string id, RequestContext requestContext, DateTimeOffset? dispatchedAt = null,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateDbSpan(
            new OutboxSpanInfo(DbSystem.Mongodb,
                _database.DatabaseNamespace.DatabaseName,
                OutboxDbOperation.MarkDispatched,
                _configuration.CollectionName),
            requestContext?.Span,
            options: _configuration.InstrumentationOptions);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            var filter = Builders<OutboxMessage>.Filter.Eq(x => x.MessageId, id);

            dispatchedAt ??= _timeProvider.GetUtcNow();
            var update = Builders<OutboxMessage>.Update
                .Set(x => x.DeliveryTime, dispatchedAt.Value.Ticks)
                .Set(x => x.DeliveredAt, dispatchedAt)
                .Unset(x => x.OutstandingCreatedTime);

            await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task MarkDispatchedAsync(IEnumerable<string> ids,
        RequestContext requestContext,
        DateTimeOffset? dispatchedAt = null,
        Dictionary<string, object>? args = null, CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateDbSpan(
            new OutboxSpanInfo(DbSystem.Mongodb,
                _database.DatabaseNamespace.DatabaseName,
                OutboxDbOperation.MarkDispatched,
                _configuration.CollectionName),
            requestContext?.Span,
            options: _configuration.InstrumentationOptions);
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            var filter = Builders<OutboxMessage>.Filter.In(x => x.MessageId, ids);

            dispatchedAt ??= _timeProvider.GetUtcNow();
            var update = Builders<OutboxMessage>.Update
                .Set(x => x.DeliveryTime, dispatchedAt.Value.Ticks)
                .Set(x => x.DeliveredAt, dispatchedAt)
                .Unset(x => x.OutstandingCreatedTime);

            await collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Message>> OutstandingMessagesAsync(TimeSpan dispatchedSince,
        RequestContext requestContext,
        int pageSize = 100,
        int pageNumber = 1, Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateDbSpan(
            new OutboxSpanInfo(DbSystem.Mongodb,
                _database.DatabaseNamespace.DatabaseName,
                OutboxDbOperation.OutStandingMessages,
                _configuration.CollectionName),
            requestContext?.Span,
            options: _configuration.InstrumentationOptions);
        try
        {
            var olderThan = _timeProvider.GetLocalNow() - dispatchedSince;
            var filter = Builders<OutboxMessage>.Filter.Lt(x => x.CreatedTime, olderThan.Ticks);
            if (args != null && args.TryGetValue("Topic", out var topic))
            {
                filter &= Builders<OutboxMessage>.Filter.Eq(x => x.Topic, topic);
            }

            var collection = await GetCollectionAsync(cancellationToken);
            var cursor = await collection.FindAsync(filter,
                    new FindOptions<OutboxMessage, OutboxMessage>
                    {
                        Limit = pageSize,
                        Skip = pageSize * Math.Max(pageNumber - 1, 0),
                        Sort = Builders<OutboxMessage>.Sort.Ascending(x => x.CreatedTime)
                    }, cancellationToken: cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

            var messages = new List<Message>(pageSize);
            while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
            {
                messages.AddRange(cursor.Current.Select(x => x.ConvertToMessage()));
            }

            return messages;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    private long? GetExpirationTime()
    {
        if (_configuration.TimeToLive.HasValue)
        {
            return _timeProvider.GetUtcNow().Add(_configuration.TimeToLive.Value).ToUnixTimeSeconds();
        }

        return null;
    }

    private ValueTask<IMongoCollection<OutboxMessage>> GetCollectionAsync(CancellationToken cancellationToken = default)
    {
        if (_collection != null)
        {
            return new ValueTask<IMongoCollection<OutboxMessage>>(_collection);
        }

        if (_configuration.OnResolvingACollection == OnResolvingACollection.Assume)
        {
            _collection =
                _database.GetCollection<OutboxMessage>(_configuration.CollectionName,
                    _configuration.CollectionSettings);
            return new ValueTask<IMongoCollection<OutboxMessage>>(_collection);
        }

        return new ValueTask<IMongoCollection<OutboxMessage>>(GetOrCreateAsync());

        async Task<IMongoCollection<OutboxMessage>> GetOrCreateAsync()
        {
            var filter = new BsonDocument("name", _configuration.CollectionName);
            var options = new ListCollectionNamesOptions { Filter = filter };

            var collections = await _database.ListCollectionNamesAsync(options, cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

            if (await collections.AnyAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext))
            {
                _collection = _database.GetCollection<OutboxMessage>(_configuration.CollectionName,
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
                _database.GetCollection<OutboxMessage>(_configuration.CollectionName,
                    _configuration.CollectionSettings);
            return _collection;
        }
    }

    /// <inheritdoc />
    public void Add(Message message, RequestContext requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<IClientSessionHandle>? transactionProvider = null)
    {
        var span = Tracer?.CreateDbSpan(
            new OutboxSpanInfo(DbSystem.Mongodb,
                _database.DatabaseNamespace.DatabaseName,
                OutboxDbOperation.Add,
                _configuration.CollectionName),
            requestContext?.Span,
            options: _configuration.InstrumentationOptions);

        try
        {
            var expiresAt = GetExpirationTime();
            var messageToStore = new OutboxMessage(message, expiresAt);
            var collection = GetCollection();

            if (transactionProvider != null)
            {
                var session = transactionProvider.GetTransaction();
                collection.InsertOneAsync(session, messageToStore);
            }
            else
            {
                collection.InsertOneAsync(messageToStore);
            }
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public void Add(IEnumerable<Message> messages, RequestContext? requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<IClientSessionHandle>? transactionProvider = null)
    {
        var span = Tracer?.CreateDbSpan(
            new OutboxSpanInfo(DbSystem.Mongodb,
                _database.DatabaseNamespace.DatabaseName,
                OutboxDbOperation.Add,
                _configuration.CollectionName),
            requestContext?.Span,
            options: _configuration.InstrumentationOptions);
        try
        {
            var expiresAt = GetExpirationTime();
            var messageItems = messages.Select(message => new OutboxMessage(message, expiresAt));
            var collection = GetCollection();
            if (transactionProvider != null)
            {
                var session = transactionProvider.GetTransaction();
                collection.InsertMany(session, messageItems);
            }
            else
            {
                collection.InsertManyAsync(messageItems);
            }
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public void Delete(string[] messageIds, RequestContext? requestContext, Dictionary<string, object>? args = null)
    {
        var span = Tracer?.CreateDbSpan(
            new OutboxSpanInfo(DbSystem.Mongodb,
                _database.DatabaseNamespace.DatabaseName,
                OutboxDbOperation.Delete,
                _configuration.CollectionName),
            requestContext?.Span,
            options: _configuration.InstrumentationOptions);
        try
        {
            var collection = GetCollection();
            var filter = Builders<OutboxMessage>.Filter.In(x => x.MessageId, messageIds);
            collection.DeleteMany(filter);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public IEnumerable<Message> DispatchedMessages(TimeSpan dispatchedSince, RequestContext requestContext,
        int pageSize = 100,
        int pageNumber = 1, int outBoxTimeout = -1, Dictionary<string, object>? args = null)
    {
        var span = Tracer?.CreateDbSpan(
            new OutboxSpanInfo(DbSystem.Mongodb,
                _database.DatabaseNamespace.DatabaseName,
                OutboxDbOperation.DispatchedMessages,
                _configuration.CollectionName),
            requestContext?.Span,
            options: _configuration.InstrumentationOptions);

        try
        {
            var olderThan = _timeProvider.GetLocalNow() - dispatchedSince;
            var filter = Builders<OutboxMessage>.Filter.Lt(x => x.DeliveryTime, olderThan.Ticks);
            if (args != null && args.TryGetValue("Topic", out var topic))
            {
                filter &= Builders<OutboxMessage>.Filter.Eq(x => x.Topic, topic);
            }

            var collection = GetCollection();
            var cursor = collection.FindSync(filter,
                new FindOptions<OutboxMessage, OutboxMessage>
                {
                    Limit = pageSize,
                    Skip = pageSize * Math.Max(pageNumber - 1, 0),
                    Sort = Builders<OutboxMessage>.Sort.Ascending(x => x.CreatedTime)
                });

            var messages = new List<Message>(pageSize);
            while (cursor.MoveNext())
            {
                messages.AddRange(cursor.Current.Select(x => x.ConvertToMessage()));
            }

            return messages;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public Message Get(string messageId, RequestContext requestContext, int outBoxTimeout = -1,
        Dictionary<string, object>? args = null)
    {
        var span = Tracer?.CreateDbSpan(
            new OutboxSpanInfo(DbSystem.Mongodb,
                _database.DatabaseNamespace.DatabaseName,
                OutboxDbOperation.Get,
                _configuration.CollectionName),
            requestContext?.Span,
            options: _configuration.InstrumentationOptions);

        try
        {
            var collection = GetCollection();
            var find = collection.FindSync(x => x.MessageId == messageId);
            if (!find.Any())
            {
                return new Message();
            }

            var first = find.First();
            return first.ConvertToMessage();
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public void MarkDispatched(string id, RequestContext requestContext, DateTimeOffset? dispatchedAt = null,
        Dictionary<string, object>? args = null)
    {
        var span = Tracer?.CreateDbSpan(
            new OutboxSpanInfo(DbSystem.Mongodb,
                _database.DatabaseNamespace.DatabaseName,
                OutboxDbOperation.MarkDispatched,
                _configuration.CollectionName),
            requestContext?.Span,
            options: _configuration.InstrumentationOptions);

        try
        {
            var collection = GetCollection();
            var filter = Builders<OutboxMessage>.Filter.Eq(x => x.MessageId, id);

            dispatchedAt ??= _timeProvider.GetUtcNow();
            var update = Builders<OutboxMessage>.Update
                .Set(x => x.DeliveryTime, dispatchedAt.Value.Ticks)
                .Set(x => x.DeliveredAt, dispatchedAt)
                .Unset(x => x.OutstandingCreatedTime);

            collection.UpdateOne(filter, update);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public IEnumerable<Message> OutstandingMessages(TimeSpan dispatchedSince, RequestContext? requestContext,
        int pageSize = 100,
        int pageNumber = 1, Dictionary<string, object>? args = null)
    {
        var span = Tracer?.CreateDbSpan(
            new OutboxSpanInfo(DbSystem.Mongodb,
                _database.DatabaseNamespace.DatabaseName,
                OutboxDbOperation.OutStandingMessages,
                _configuration.CollectionName),
            requestContext?.Span,
            options: _configuration.InstrumentationOptions);
        try
        {
            var olderThan = _timeProvider.GetLocalNow() - dispatchedSince;
            var filter = Builders<OutboxMessage>.Filter.Lt(x => x.CreatedTime, olderThan.Ticks);
            if (args != null && args.TryGetValue("Topic", out var topic))
            {
                filter &= Builders<OutboxMessage>.Filter.Eq(x => x.Topic, topic);
            }

            var collection = GetCollection();
            var cursor = collection.FindSync(filter,
                new FindOptions<OutboxMessage, OutboxMessage>
                {
                    Limit = pageSize,
                    Skip = pageSize * Math.Max(pageNumber - 1, 0),
                    Sort = Builders<OutboxMessage>.Sort.Ascending(x => x.CreatedTime)
                });

            var messages = new List<Message>(pageSize);
            while (cursor.MoveNext())
            {
                messages.AddRange(cursor.Current.Select(x => x.ConvertToMessage()));
            }

            return messages;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    private IMongoCollection<OutboxMessage> GetCollection()
    {
        if (_collection != null)
        {
            return _collection;
        }

        if (_configuration.OnResolvingACollection == OnResolvingACollection.Assume)
        {
            _collection =
                _database.GetCollection<OutboxMessage>(_configuration.CollectionName,
                    _configuration.CollectionSettings);
            return _collection;
        }

        var filter = new BsonDocument("name", _configuration.CollectionName);
        var options = new ListCollectionNamesOptions { Filter = filter };

        var collections = _database.ListCollectionNames(options);
        if (collections.Any())
        {
            _collection = _database.GetCollection<OutboxMessage>(_configuration.CollectionName,
                _configuration.CollectionSettings);
            return _collection;
        }

        if (_configuration.OnResolvingACollection == OnResolvingACollection.Validate)
        {
            throw new InvalidOperationException("collection not exits");
        }

        using var session = _client.StartSession();

        _database
            .CreateCollection(session, _configuration.CollectionName, _configuration.CreateCollectionOptions);

        _collection =
            _database.GetCollection<OutboxMessage>(_configuration.CollectionName,
                _configuration.CollectionSettings);
        return _collection;
    }
}
