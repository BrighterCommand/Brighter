using MongoDB.Driver;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.MongoDb;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Inbox.MongoDb;

/// <summary>
/// The inbox implementation to MongoDB 
/// </summary>
public class MongoDbInbox : BaseMongoDb<InboxMessage>, IAmAnInboxAsync, IAmAnInboxSync
{
    /// <summary>
    /// Initialize a new instance of <see cref="MongoDbInbox"/>.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    public MongoDbInbox(MongoDbConfiguration configuration)
        : base(configuration)
    {
    }

    /// <inheritdoc />
    public bool ContinueOnCapturedContext { get; set; }

    /// <inheritdoc />
    public IAmABrighterTracer? Tracer { private get; set; }

    /// <summary>
    ///   Awaitably adds a command to the store.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="command">The command.</param>
    /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
    /// <param name="requestContext">What is the context for this request; used to access the Span</param>
    /// <param name="timeoutInMilliseconds">Timeout is ignored as the timeout is handled by the MongoDb SDK</param>
    /// <param name="cancellationToken">Allow the sender to cancel the operation, if the parameter is supplied</param>
    /// <returns><see cref="Task"/>.</returns>
    public async Task AddAsync<T>(T command, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1,
        CancellationToken cancellationToken = default) where T : class, IRequest
    {
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.command.id", command.Id}
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
            var message = new InboxMessage(command, command.Id, contextKey, Configuration.TimeProvider.GetUtcNow(),
                ExpireAfterSeconds);
            await Collection.InsertOneAsync(message, cancellationToken: cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }
        catch (MongoWriteException e)
        {
            if (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return;
            }

            throw;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <summary>
    ///   Awaitably finds a command with the specified identifier.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="id">The identifier.</param>
    /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
    /// <param name="requestContext">What is the context for this request; used to access the Span</param>
    /// <param name="timeoutInMilliseconds">Timeout is ignored as the timeout is handled by the MongoDb SDK</param>
    /// <param name="cancellationToken">Allow the sender to cancel the operation, if the parameter is supplied</param>
    /// <returns><see cref="Task{T}"/>.</returns>
    public async Task<T> GetAsync<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1,
        CancellationToken cancellationToken = default) where T : class, IRequest
    {
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.command.id", id}
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
            var commandId = new InboxMessage.InboxMessageId { Id = id, ContextKey = contextKey };
            var filter = Builders<InboxMessage>.Filter.Eq(x => x.Id, commandId);

            var command = await Collection.Find(filter)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);

            if (command == null)
            {
                throw new RequestNotFoundException<T>(id);
            }

            return command.ToCommand<T>();
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <summary>
    ///   Awaitable checks whether a command with the specified identifier exists in the store.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="id">The identifier.</param>
    /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
    /// <param name="requestContext">What is the context for this request; used to access the Span</param>
    /// <param name="timeoutInMilliseconds">Timeout is ignored as the timeout is handled by the MongoDb SDK</param>
    /// <param name="cancellationToken">Allow the sender to cancel the operation, if the parameter is supplied</param>
    /// <returns><see cref="Task{true}"/> if it exists, otherwise <see cref="Task{false}"/>.</returns>
    public async Task<bool> ExistsAsync<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1,
        CancellationToken cancellationToken = default) where T : class, IRequest
    {
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.command.id", id}
        };
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.Exists,
                Configuration.CollectionName,
                dbAttributes: dbAttributes),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);

        try
        {
            var commandId = new InboxMessage.InboxMessageId { Id = id, ContextKey = contextKey };
            var filter = Builders<InboxMessage>.Filter.Eq("Id", commandId);
            return await Collection.Find(filter)
                .AnyAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(ContinueOnCapturedContext);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <summary>
    ///   Adds a command to the store.
    ///   Will block, and consume another thread for callback on threadpool; use within sync pipeline only.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="command">The command.</param>
    /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
    /// <param name="requestContext">What is the context for this request; used to access the Span</param>
    /// <param name="timeoutInMilliseconds">Timeout is ignored as the timeout is handled by the MongoDb SDK</param>
    public void Add<T>(T command, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1) where T : class, IRequest
    {
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.command.id", command.Id}
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
            var message = new InboxMessage(command, command.Id, contextKey, Configuration.TimeProvider.GetUtcNow(),
                ExpireAfterSeconds);
            Collection.InsertOne(message);
        }
        catch (MongoWriteException e)
        {
            if (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return;
            }

            throw;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <summary>
    ///   Finds a command with the specified identifier.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="id">The identifier.</param>
    /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
    /// <param name="requestContext">What is the context for this request; used to access the Span</param>
    /// <param name="timeoutInMilliseconds">Timeout is ignored as the timeout is handled by the MongoDb SDK</param>
    /// <returns><see cref="T"/></returns>
    public T Get<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1) where T : class, IRequest
    {
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.command.id", id}
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
            var commandId = new InboxMessage.InboxMessageId { Id = id, ContextKey = contextKey };
            var filter = Builders<InboxMessage>.Filter.Eq(x => x.Id, commandId);

            var command = Collection.Find(filter).FirstOrDefault();
            if (command == null)
            {
                throw new RequestNotFoundException<T>(id);
            }

            return command.ToCommand<T>();
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <summary>
    ///   Checks whether a command with the specified identifier exists in the store.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="id">The identifier.</param>
    /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
    /// <param name="requestContext">What is the context for this request; used to access the Span</param>
    /// <param name="timeoutInMilliseconds">Timeout is ignored as the timeout is handled by the MongoDb SDK</param>
    /// <returns><see langword="true"/> if it exists, otherwise <see langword="false"/>.</returns>
    public bool Exists<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1) where T : class, IRequest
    {
        var dbAttributes = new Dictionary<string, string>()
        {
            {"db.operation.parameter.command.id", id}
        };
        var span = Tracer?.CreateDbSpan(
            new BoxSpanInfo(DbSystem.Mongodb,
                Configuration.DatabaseName,
                BoxDbOperation.Exists,
                Configuration.CollectionName,
                dbAttributes: dbAttributes),
            requestContext?.Span,
            options: Configuration.InstrumentationOptions);

        try
        {
            var commandId = new InboxMessage.InboxMessageId { Id = id, ContextKey = contextKey };
            var filter = Builders<InboxMessage>.Filter.Eq("Id", commandId);
            return Collection.Find(filter)
                .Any();
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }
}
