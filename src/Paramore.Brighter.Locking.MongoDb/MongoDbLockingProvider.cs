using System.Collections.ObjectModel;
using MongoDB.Driver;
using Paramore.Brighter.MongoDb;

namespace Paramore.Brighter.Locking.MongoDb;

/// <summary>
/// The MongoDb implementation to <see cref="IDistributedLock"/>
/// </summary>
public class MongoDbLockingProvider : BaseMongoDb<LockMessage>, IDistributedLock
{
    /// <summary>
    /// Initialize new instance of <see cref="MongoDbLockingProvider"/>
    /// </summary>
    /// <param name="configuration">The <see cref="MongoDbConfiguration"/></param>
    public MongoDbLockingProvider(MongoDbConfiguration configuration)
        : base(configuration)
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
