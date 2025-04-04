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

    /// <summary>
    /// Returns all messages in the store
    /// </summary>
    /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
    /// <param name="pageNumber">Page number of results to return (default = 1)</param>
    /// <param name="args">Additional parameters required for search, if any</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A list of messages</returns>
    public async Task<IList<Message>> GetAsync(int pageSize = 100,
        int pageNumber = 1,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.Get,
                Configuration.CollectionName),
            null,
            options: Configuration.InstrumentationOptions);

        try
        {
            var filter = Builders<OutboxMessage>.Filter.Empty;
            if (args != null && args.TryGetValue("Topic", out var topic))
            {
                filter &= Builders<OutboxMessage>.Filter.Eq(x => x.Topic, topic);
            }

            var cursor = await Collection.FindAsync(filter,
                    new FindOptions<OutboxMessage> { Skip = pageSize * Math.Max(pageNumber - 1, 0), Limit = pageSize },
                    cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

            var messages = new List<Message>(pageSize);
            while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
            {
                messages.AddRange(cursor.Current.Select(x => x.ConvertToMessage()));
            }

            span?.AddTag("db.response.returned_rows", messages.Count);
            return messages;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <summary>
    /// Returns messages specified by the Ids
    /// </summary>
    /// <param name="messageIds">The Ids of the messages</param>
    /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
    /// <param name="outBoxTimeout">The Timeout of the outbox.</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    /// <returns></returns>
    public async Task<IEnumerable<Message>> GetAsync(
        IEnumerable<string> messageIds,
        RequestContext requestContext,
        int outBoxTimeout = -1,
        CancellationToken cancellationToken = default
    )
    {
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.message.ids", string.Join(",", messageIds)}
        };
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.Get,
                Configuration.CollectionName,
                dbAttributes: dbAttributes),
            null,
            options: Configuration.InstrumentationOptions);

        try
        {
            var filter = Builders<OutboxMessage>.Filter.In(x => x.MessageId, messageIds);

            var cursor = await Collection.FindAsync(filter,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

            var messages = new List<Message>();
            while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
            {
                messages.AddRange(cursor.Current.Select(x => x.ConvertToMessage()));
            }

            span?.AddTag("db.response.returned_rows", messages.Count);
            return messages;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <summary>
    /// Get the number of messages in the Outbox that are not dispatched
    /// </summary>
    /// <param name="cancellationToken">Cancel the async operation</param>
    /// <returns></returns>
    public async Task<long> GetNumberOfOutstandingMessagesAsync(CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.Get,
                Configuration.CollectionName),
            null,
            options: Configuration.InstrumentationOptions);

        try
        {
            return await Collection.CountDocumentsAsync(
                    Builders<OutboxMessage>.Filter.Eq(x => x.Dispatched, null),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task AddAsync(Message message,
        RequestContext requestContext,
        int outBoxTimeout = -1,
        IAmABoxTransactionProvider<IClientSessionHandle>? transactionProvider = null,
        CancellationToken cancellationToken = default)
    {
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.message.id", message.Id}
        };
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.Add,
                Configuration.CollectionName,
                dbAttributes: dbAttributes),
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
        catch (MongoWriteException e)
        {
            if (e.WriteError.Category != ServerErrorCategory.DuplicateKey)
            {
                throw;
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
        var dbAttributes = new Dictionary<string, string>()
        {
          {"db.operation.parameter.message.ids", string.Join(",", messages.Select(m => m.Id))}
        };
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.Add,
                Configuration.CollectionName,
                dbAttributes: dbAttributes),
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
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.message.ids", string.Join(",", messageIds)}
        };
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.Delete,
                Configuration.CollectionName,
                dbAttributes: dbAttributes),
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
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.DispatchedMessages,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);
        try
        {
            var olderThan = Configuration.TimeProvider.GetLocalNow() - dispatchedSince;
            var filter = Builders<OutboxMessage>.Filter.Lt(x => x.Dispatched, olderThan);
            if (args != null && args.TryGetValue("Topic", out var topic))
            {
                filter &= Builders<OutboxMessage>.Filter.Eq(x => x.Topic, topic);
            }

            var cursor = await Collection.FindAsync(filter,
                    new FindOptions<OutboxMessage, OutboxMessage>
                    {
                        Limit = pageSize,
                        Skip = pageSize * Math.Max(pageNumber - 1, 0),
                        Sort = Builders<OutboxMessage>.Sort.Ascending(x => x.TimeStamp)
                    }, cancellationToken: cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

            var messages = new List<Message>(pageSize);
            while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
            {
                messages.AddRange(cursor.Current.Select(x => x.ConvertToMessage()));
            }

            span?.AddTag("db.response.returned_rows", messages.Count);
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
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.message.id", messageId}
        };
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.Get,
                Configuration.CollectionName,
                dbAttributes: dbAttributes),
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
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.message.id", id}
        };
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.MarkDispatched,
                Configuration.CollectionName,
                dbAttributes: dbAttributes),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);

        try
        {
            var filter = Builders<OutboxMessage>.Filter.Eq(x => x.MessageId, id);

            dispatchedAt ??= Configuration.TimeProvider.GetUtcNow();
            var update = Builders<OutboxMessage>.Update
                .Set(x => x.Dispatched, dispatchedAt.Value);

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
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.message.ids", string.Join(",", ids)}
        };
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.MarkDispatched,
                Configuration.CollectionName,
                dbAttributes: dbAttributes),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);
        try
        {
            var filter = Builders<OutboxMessage>.Filter.In(x => x.MessageId, ids);

            dispatchedAt ??= Configuration.TimeProvider.GetUtcNow();
            var update = Builders<OutboxMessage>.Update
                .Set(x => x.Dispatched, dispatchedAt.Value);

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
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.OutStandingMessages,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);
        try
        {
            var olderThan = Configuration.TimeProvider.GetLocalNow() - dispatchedSince;
            var filter = Builders<OutboxMessage>.Filter.Eq(x => x.Dispatched, null);
            filter &= Builders<OutboxMessage>.Filter.Lt(x => x.TimeStamp, olderThan);
            if (args != null && args.TryGetValue("Topic", out var topic))
            {
                filter &= Builders<OutboxMessage>.Filter.Eq(x => x.Topic, topic);
            }

            var cursor = await Collection.FindAsync(filter,
                    new FindOptions<OutboxMessage, OutboxMessage>
                    {
                        Limit = pageSize,
                        Skip = pageSize * Math.Max(pageNumber - 1, 0),
                        Sort = Builders<OutboxMessage>.Sort.Ascending(x => x.TimeStamp)
                    }, cancellationToken: cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

            var messages = new List<Message>(pageSize);
            while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(ContinueOnCapturedContext))
            {
                messages.AddRange(cursor.Current.Select(x => x.ConvertToMessage()));
            }

            span?.AddTag("db.response.returned_rows", messages.Count);
            return messages;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <summary>
    /// Returns all messages in the store
    /// </summary>
    /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
    /// <param name="pageNumber">Page number of results to return (default = 1)</param>
    /// <param name="args">Additional parameters required for search, if any</param>
    /// <returns>A list of messages</returns>
    public IList<Message> Get(int pageSize = 100, int pageNumber = 1, Dictionary<string, object>? args = null)
    {
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.Get,
                Configuration.CollectionName),
            null,
            options: Configuration.InstrumentationOptions);

        try
        {
            var filter = Builders<OutboxMessage>.Filter.Empty;
            if (args != null && args.TryGetValue("Topic", out var topic))
            {
                filter &= Builders<OutboxMessage>.Filter.Eq(x => x.Topic, topic);
            }

            var cursor = Collection.FindSync(filter,
                new FindOptions<OutboxMessage> { Skip = pageSize * Math.Max(pageNumber - 1, 0), Limit = pageSize });

            var messages = new List<Message>(pageSize);
            while (cursor.MoveNext())
            {
                messages.AddRange(cursor.Current.Select(x => x.ConvertToMessage()));
            }

            span?.AddTag("db.response.returned_rows", messages.Count);
            return messages;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <summary>
    /// Returns messages specified by the Ids
    /// </summary>
    /// <param name="messageIds">The Ids of the messages</param>
    /// <param name="requestContext">What is the context for this request; used to access the Span</param>        
    /// <param name="outBoxTimeout">The Timeout of the outbox.</param>
    /// <returns></returns>
    public IEnumerable<Message> Get(
        IEnumerable<string> messageIds,
        RequestContext? requestContext = null,
        int outBoxTimeout = -1
    )
    {
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.message.ids", string.Join(",", messageIds)}
        };
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.Get,
                Configuration.CollectionName,
                dbAttributes: dbAttributes),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);

        try
        {
            var filter = Builders<OutboxMessage>.Filter.In(x => x.MessageId, messageIds);

            var cursor = Collection.FindSync(filter);

            var messages = new List<Message>();
            while (cursor.MoveNext())
            {
                messages.AddRange(cursor.Current.Select(x => x.ConvertToMessage()));
            }

            span?.AddTag("db.response.returned_rows", messages.Count);
            return messages;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <summary>
    /// Get the number of messages in the Outbox that are not dispatched
    /// </summary>
    /// <returns></returns>
    public long GetNumberOfOutstandingMessages()
    {
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.Get,
                Configuration.CollectionName),
            null,
            options: Configuration.InstrumentationOptions);

        try
        {
            return Collection.CountDocuments(Builders<OutboxMessage>.Filter.Eq(x => x.Dispatched, null));
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
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.message.id", message.Id}
        };
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.Add,
                Configuration.CollectionName,
                dbAttributes: dbAttributes),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);

        try
        {
            var messageToStore = new OutboxMessage(message, ExpireAfterSeconds);

            if (transactionProvider != null)
            {
                var session = transactionProvider.GetTransaction();
                Collection.InsertOne(session, messageToStore);
            }
            else
            {
                Collection.InsertOne(messageToStore);
            }
        }
        catch (MongoWriteException e)
        {
            if (e.WriteError.Category != ServerErrorCategory.DuplicateKey)
            {
                throw;
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
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.message.ids", string.Join(",", messages.Select(m => m.Id))}
        };
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.Add,
                Configuration.CollectionName,
                dbAttributes: dbAttributes),
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
                Collection.InsertMany(messageItems);
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
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.message.ids", string.Join(",", messageIds)}
        };
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.Delete,
                Configuration.CollectionName,
                dbAttributes: dbAttributes),
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
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.DispatchedMessages,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);

        try
        {
            var olderThan = Configuration.TimeProvider.GetLocalNow() - dispatchedSince;
            var filter = Builders<OutboxMessage>.Filter.Lt(x => x.Dispatched, olderThan);
            if (args != null && args.TryGetValue("Topic", out var topic))
            {
                filter &= Builders<OutboxMessage>.Filter.Eq(x => x.Topic, topic);
            }

            var cursor = Collection.FindSync(filter,
                new FindOptions<OutboxMessage, OutboxMessage>
                {
                    Limit = pageSize,
                    Skip = pageSize * Math.Max(pageNumber - 1, 0),
                    Sort = Builders<OutboxMessage>.Sort.Ascending(x => x.TimeStamp)
                });

            var messages = new List<Message>(pageSize);
            while (cursor.MoveNext())
            {
                messages.AddRange(cursor.Current.Select(x => x.ConvertToMessage()));
            }

            span?.AddTag("db.response.returned_rows", messages.Count);
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
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.message.id", messageId}
        };
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.Get,
                Configuration.CollectionName,
                dbAttributes: dbAttributes),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);

        try
        {
            var find = Collection.FindSync(x => x.MessageId == messageId);
            var first = find.FirstOrDefault();
            return first?.ConvertToMessage() ?? new Message();
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
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.message.id", id}
        };
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.MarkDispatched,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);

        try
        {
            var filter = Builders<OutboxMessage>.Filter.Eq(x => x.MessageId, id);

            dispatchedAt ??= Configuration.TimeProvider.GetUtcNow();
            var update = Builders<OutboxMessage>.Update
                .Set(x => x.Dispatched, dispatchedAt.Value);

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
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.OutStandingMessages,
                Configuration.CollectionName),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);
        try
        {
            var olderThan = Configuration.TimeProvider.GetLocalNow() - dispatchedSince;
            var filter = Builders<OutboxMessage>.Filter.Eq(x => x.Dispatched, null);
            filter &= Builders<OutboxMessage>.Filter.Lt(x => x.TimeStamp, olderThan);
            if (args != null && args.TryGetValue("Topic", out var topic))
            {
                filter &= Builders<OutboxMessage>.Filter.Eq(x => x.Topic, topic);
            }

            var cursor = Collection.FindSync(filter,
                new FindOptions<OutboxMessage, OutboxMessage>
                {
                    Limit = pageSize,
                    Skip = pageSize * Math.Max(pageNumber - 1, 0),
                    Sort = Builders<OutboxMessage>.Sort.Ascending(x => x.TimeStamp)
                });

            var messages = new List<Message>(pageSize);
            while (cursor.MoveNext())
            {
                messages.AddRange(cursor.Current.Select(x => x.ConvertToMessage()));
            }

            span?.AddTag("db.response.returned_rows", messages.Count);
            return messages;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }
}
