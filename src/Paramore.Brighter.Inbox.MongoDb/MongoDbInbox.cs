using MongoDB.Driver;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.MongoDb;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Inbox.MongoDb;

/// <summary>
/// Implements the Brighter Inbox pattern using MongoDB as the backing store.
/// This class handles both asynchronous and synchronous operations for adding,
/// retrieving, and checking the existence of messages in the inbox, ensuring
/// idempotent message processing. It leverages MongoDB's TTL feature for message expiration.
/// </summary>
public class MongoDbInbox : BaseMongoDb<InboxMessage>, IAmAnInboxAsync, IAmAnInboxSync
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDbInbox"/> class with explicit
    /// connection and configuration providers.
    /// </summary>
    /// <param name="connectionProvider">The MongoDB connection provider.</param>
    /// <param name="configuration">The overall MongoDB configuration, which must include inbox settings.</param>
    /// <exception cref="ArgumentException">Thrown if the inbox configuration is null.</exception>
    public MongoDbInbox(IAmAMongoDbConnectionProvider connectionProvider, IAmAMongoDbConfiguration configuration)
        : base(connectionProvider, configuration, configuration.Inbox ?? throw new ArgumentException("Inbox can't be null"))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDbInbox"/> class using only the
    /// main MongoDB configuration. A <see cref="MongoDbConnectionProvider"/> will be
    /// created internally.
    /// </summary>
    /// <param name="configuration">The overall MongoDB configuration, which must include inbox settings.</param>
    public MongoDbInbox(IAmAMongoDbConfiguration configuration)
        : this(new MongoDbConnectionProvider(configuration), configuration)
    {
        
    }
    

    /// <inheritdoc />
    public bool ContinueOnCapturedContext { get; set; }

    /// <inheritdoc />
    public IAmABrighterTracer? Tracer { private get; set; }

    /// <inheritdoc />
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
                CollectionConfiguration.Name,
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

   
    /// <inheritdoc />
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
                CollectionConfiguration.Name,
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

    /// <inheritdoc />
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
                CollectionConfiguration.Name,
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

    /// <inheritdoc />
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
                CollectionConfiguration.Name,
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

    /// <inheritdoc />
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
                CollectionConfiguration.Name,
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

    /// <inheritdoc />
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
                CollectionConfiguration.Name,
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
