using MongoDB.Driver;
using Paramore.Brighter.MongoDb;

namespace Paramore.Brighter.Locking.MongoDb;

/// <summary>
/// Implements a distributed lock mechanism using MongoDB as the backing store.
/// This class extends <see cref="BaseMongoDb{TCollection}"/> to manage a collection
/// of <see cref="LockMessage"/> documents, enabling coordinated access to shared resources
/// across distributed services.
/// </summary>
public class MongoDbLockingProvider : BaseMongoDb<LockMessage>, IDistributedLock
{ 
    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDbLockingProvider"/> class
    /// with explicit connection and configuration providers.
    /// </summary>
    /// <param name="connectionProvider">The MongoDB connection provider.</param>
    /// <param name="configuration">The overall MongoDB configuration, which must include locking settings.</param>
    /// <exception cref="ArgumentException">Thrown if the locking configuration is null.</exception>
    public MongoDbLockingProvider(IAmAMongoDbConnectionProvider connectionProvider, IAmAMongoDbConfiguration configuration)
        : base(connectionProvider, configuration, configuration.Locking ?? throw new ArgumentException("Locking can't be null"))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDbLockingProvider"/> class
    /// using only the main MongoDB configuration. A <see cref="MongoDbConnectionProvider"/>
    /// will be created internally.
    /// </summary>
    /// <param name="configuration">The overall MongoDB configuration, which must include locking settings.</param>
    public MongoDbLockingProvider(IAmAMongoDbConfiguration configuration)
        : this(new MongoDbConnectionProvider(configuration), configuration)
    {
        
    }

    /// <inheritdoc />
    public async Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken)
    {
        try
        {
            await Collection.InsertOneAsync(new LockMessage
                {
                    Id = resource,
                    TimeStamp = Configuration.TimeProvider.GetUtcNow(),
                    ExpireAfterSeconds = ExpireAfterSeconds
                },
                cancellationToken: cancellationToken);
            return resource;
        }
        catch (MongoWriteException e)
        {
            if (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return null;
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken)
        => await Collection.DeleteOneAsync(Builders<LockMessage>.Filter.Eq(x => x.Id, lockId), cancellationToken);
}
