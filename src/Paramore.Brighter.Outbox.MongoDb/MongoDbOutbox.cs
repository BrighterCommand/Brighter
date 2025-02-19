using MongoDB.Bson;
using MongoDB.Driver;
using Paramore.Brighter.MongoDb;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Outbox.MongoDb;

/// <summary>
/// The implementation for MongoDB for outbox
/// </summary>
public class MongoDbOutbox : BaseMongoDb<OutboxMessage>, IAmAnOutboxAsync<Message, IClientSessionHandle>,
    IAmAnOutboxSync<Message, IClientSessionHandle>
{
    /// <summary>
    /// Initialize MongoDbOutbox
    /// </summary>
    /// <param name="configuration">The <see cref="MongoDbConfiguration"/>.</param>
    public MongoDbOutbox(MongoDbConfiguration configuration)
        : base(configuration)
    {
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
                Configuration.DatabaseName,
                OutboxDbOperation.Add,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);

        try
        {
            var messageToStore = new OutboxMessage(message, ExpireAfterSeconds);

            if (transactionProvider != null)
            {
                var session = await transactionProvider.GetTransactionAsync(cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);

                await Collection
                    .InsertOneAsync(session, messageToStore, cancellationToken: cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);
            }
            else
            {
                await Collection
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
                Configuration.DatabaseName,
                OutboxDbOperation.Add,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);
        try
        {
            var messageItems = messages.Select(message => new OutboxMessage(message, ExpireAfterSeconds));
            if (transactionProvider != null)
            {
                var session = await transactionProvider.GetTransactionAsync(cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);
                await Collection
                    .InsertManyAsync(session, messageItems, cancellationToken: cancellationToken)
                    .ConfigureAwait(ContinueOnCapturedContext);
            }
            else
            {
                await Collection
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
                Configuration.DatabaseName,
                OutboxDbOperation.Delete,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);

        try
        {
            var filter = Builders<OutboxMessage>.Filter.In(x => x.MessageId, messageIds);
            await Collection.DeleteManyAsync(filter, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
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
                Configuration.DatabaseName,
                OutboxDbOperation.DispatchedMessages,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);
        try
        {
            var olderThan = Configuration.TimeProvider.GetLocalNow() - dispatchedSince;
            var filter = Builders<OutboxMessage>.Filter.Lt(x => x.DeliveryTime, olderThan.Ticks);
            if (args != null && args.TryGetValue("Topic", out var topic))
            {
                filter &= Builders<OutboxMessage>.Filter.Eq(x => x.Topic, topic);
            }

            var cursor = await Collection.FindAsync(filter,
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
                Configuration.DatabaseName,
                OutboxDbOperation.Get,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);

        try
        {
            var find = await Collection
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
                Configuration.DatabaseName,
                OutboxDbOperation.MarkDispatched,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);

        try
        {
            var filter = Builders<OutboxMessage>.Filter.Eq(x => x.MessageId, id);

            dispatchedAt ??= Configuration.TimeProvider.GetUtcNow();
            var update = Builders<OutboxMessage>.Update
                .Set(x => x.DeliveryTime, dispatchedAt.Value.Ticks)
                .Set(x => x.DeliveredAt, dispatchedAt)
                .Unset(x => x.OutstandingCreatedTime);

            await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
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
                Configuration.DatabaseName,
                OutboxDbOperation.MarkDispatched,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);
        try
        {
            var filter = Builders<OutboxMessage>.Filter.In(x => x.MessageId, ids);

            dispatchedAt ??= Configuration.TimeProvider.GetUtcNow();
            var update = Builders<OutboxMessage>.Update
                .Set(x => x.DeliveryTime, dispatchedAt.Value.Ticks)
                .Set(x => x.DeliveredAt, dispatchedAt)
                .Unset(x => x.OutstandingCreatedTime);

            await Collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken)
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
                Configuration.DatabaseName,
                OutboxDbOperation.OutStandingMessages,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);
        try
        {
            var olderThan = Configuration.TimeProvider.GetLocalNow() - dispatchedSince;
            var filter = Builders<OutboxMessage>.Filter.Lt(x => x.CreatedTime, olderThan.Ticks);
            if (args != null && args.TryGetValue("Topic", out var topic))
            {
                filter &= Builders<OutboxMessage>.Filter.Eq(x => x.Topic, topic);
            }

            var cursor = await Collection.FindAsync(filter,
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
    public void Add(Message message, RequestContext requestContext, int outBoxTimeout = -1,
        IAmABoxTransactionProvider<IClientSessionHandle>? transactionProvider = null)
    {
        var span = Tracer?.CreateDbSpan(
            new OutboxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                OutboxDbOperation.Add,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);

        try
        {
            var messageToStore = new OutboxMessage(message, ExpireAfterSeconds);

            if (transactionProvider != null)
            {
                var session = transactionProvider.GetTransaction();
                Collection.InsertOneAsync(session, messageToStore);
            }
            else
            {
                Collection.InsertOneAsync(messageToStore);
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
                Configuration.DatabaseName,
                OutboxDbOperation.Add,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);
        try
        {
            var messageItems = messages.Select(message => new OutboxMessage(message, ExpireAfterSeconds));
            if (transactionProvider != null)
            {
                var session = transactionProvider.GetTransaction();
                Collection.InsertMany(session, messageItems);
            }
            else
            {
                Collection.InsertManyAsync(messageItems);
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
                Configuration.DatabaseName,
                OutboxDbOperation.Delete,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);
        try
        {
            var filter = Builders<OutboxMessage>.Filter.In(x => x.MessageId, messageIds);
            Collection.DeleteMany(filter);
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
                Configuration.DatabaseName,
                OutboxDbOperation.DispatchedMessages,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);

        try
        {
            var olderThan = Configuration.TimeProvider.GetLocalNow() - dispatchedSince;
            var filter = Builders<OutboxMessage>.Filter.Lt(x => x.DeliveryTime, olderThan.Ticks);
            if (args != null && args.TryGetValue("Topic", out var topic))
            {
                filter &= Builders<OutboxMessage>.Filter.Eq(x => x.Topic, topic);
            }

            var cursor = Collection.FindSync(filter,
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
                Configuration.DatabaseName,
                OutboxDbOperation.Get,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);

        try
        {
            var find = Collection.FindSync(x => x.MessageId == messageId);
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
                Configuration.DatabaseName,
                OutboxDbOperation.MarkDispatched,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);

        try
        {
            var filter = Builders<OutboxMessage>.Filter.Eq(x => x.MessageId, id);

            dispatchedAt ??= Configuration.TimeProvider.GetUtcNow();
            var update = Builders<OutboxMessage>.Update
                .Set(x => x.DeliveryTime, dispatchedAt.Value.Ticks)
                .Set(x => x.DeliveredAt, dispatchedAt)
                .Unset(x => x.OutstandingCreatedTime);

            Collection.UpdateOne(filter, update);
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
                Configuration.DatabaseName,
                OutboxDbOperation.OutStandingMessages,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);
        try
        {
            var olderThan = Configuration.TimeProvider.GetLocalNow() - dispatchedSince;
            var filter = Builders<OutboxMessage>.Filter.Lt(x => x.CreatedTime, olderThan.Ticks);
            if (args != null && args.TryGetValue("Topic", out var topic))
            {
                filter &= Builders<OutboxMessage>.Filter.Eq(x => x.Topic, topic);
            }

            var cursor = Collection.FindSync(filter,
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
}
