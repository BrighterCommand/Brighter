﻿using MongoDB.Driver;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.MongoDb;

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
    public async Task AddAsync<T>(T command, string contextKey, int timeoutInMilliseconds = -1,
        CancellationToken cancellationToken = default) where T : class, IRequest
    {
        var message = new InboxMessage(command, command.Id, contextKey, Configuration.TimeProvider.GetUtcNow(),
            ExpireAfterSeconds);

        try
        {
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
    }


    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string id, string contextKey, int timeoutInMilliseconds = -1,
        CancellationToken cancellationToken = default) where T : class, IRequest
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

    /// <inheritdoc />
    public async Task<bool> ExistsAsync<T>(string id, string contextKey, int timeoutInMilliseconds = -1,
        CancellationToken cancellationToken = default) where T : class, IRequest
    {
        var commandId = new InboxMessage.InboxMessageId { Id = id, ContextKey = contextKey };
        var filter = Builders<InboxMessage>.Filter.Eq("Id", commandId);
        return await Collection.Find(filter)
            .AnyAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(ContinueOnCapturedContext);
    }

    /// <inheritdoc />
    public void Add<T>(T command, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
    {
        var message = new InboxMessage(command, command.Id, contextKey, Configuration.TimeProvider.GetUtcNow(),
            ExpireAfterSeconds);

        try
        {
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
    }

    /// <inheritdoc />
    public T Get<T>(string id, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
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

    /// <inheritdoc />
    public bool Exists<T>(string id, string contextKey, int timeoutInMilliseconds = -1) where T : class, IRequest
    {
        var commandId = new InboxMessage.InboxMessageId { Id = id, ContextKey = contextKey };
        var filter = Builders<InboxMessage>.Filter.Eq("Id", commandId);
        return Collection.Find(filter)
            .Any();
    }
}
