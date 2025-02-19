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
        var update = Builders<LockMessage>.Update.SetOnInsert(x => x.Id, resource);
        if (ExpireAfterSeconds != null)
        {
            update = update.SetOnInsert(x => x.ExpireAfterSeconds, ExpireAfterSeconds);
        }
        
        var doc= await Collection.FindOneAndUpdateAsync(Builders<LockMessage>.Filter.Eq(x => x.Id, resource),
            update,
            new FindOneAndUpdateOptions<LockMessage> { IsUpsert = true, ReturnDocument = ReturnDocument.Before }, cancellationToken);

        return doc?.Id;
    }

    /// <inheritdoc />
    public async Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken) 
        => await Collection.DeleteOneAsync(Builders<LockMessage>.Filter.Eq(x => x.Id, lockId), cancellationToken);
}
