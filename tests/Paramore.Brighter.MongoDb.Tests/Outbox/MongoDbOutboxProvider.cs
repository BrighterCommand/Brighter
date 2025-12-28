
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using Paramore.Brighter.MongoDB.Tests.Outbox.Async;
using Paramore.Brighter.MongoDB.Tests.Outbox.Sync;
using Paramore.Brighter.Outbox.MongoDb;

namespace Paramore.Brighter.MongoDb.Tests.Outbox;

public class MongoDbOutboxProvider : IAmAnOutboxProviderSync, IAmAnOutboxProviderAsync
{
    private readonly string _collectionName = $"Outbox{Uuid.New():N}";

    public IAmAnOutboxSync<Message, IClientSessionHandle> CreateOutbox()
    {
        return new MongoDbOutbox(new MongoDbConfiguration(Const.Client, Const.DatabaseName)
        {
            Outbox = new MongoDbCollectionConfiguration
            {
                Name = _collectionName
            }
        });
    }

    public IAmAnOutboxAsync<Message, IClientSessionHandle> CreateOutboxAsync()
    {
        return new MongoDbOutbox(new MongoDbConfiguration(Const.Client, Const.DatabaseName)
        {
            Outbox = new MongoDbCollectionConfiguration
            {
                Name = _collectionName
            }
        });
    }

    public void CreateStore()
    {
        // The collection is created automatically when the first message is added.
    }

    public Task CreateStoreAsync()
    {
        // The collection is created automatically when the first message is added.
        return Task.CompletedTask;
    }

    public IAmABoxTransactionProvider<IClientSessionHandle> CreateTransactionProvider()
    {
        return new MongoDbUnitOfWork(new MongoDbConfiguration(Const.Client, Const.DatabaseName)
        {
            Outbox = new MongoDbCollectionConfiguration
            {
                Name = _collectionName
            }
        });
    }

    public void DeleteStore(IEnumerable<Message> messages)
    {
        var database = Const.Client.GetDatabase(Const.DatabaseName);
        database.DropCollection(_collectionName);
    }

    public async Task DeleteStoreAsync(IEnumerable<Message> messages)
    {
        var database = Const.Client.GetDatabase(Const.DatabaseName);
        await database.DropCollectionAsync(_collectionName);
    }

    public IEnumerable<Message> GetAllMessages()
    {
        var outbox = new MongoDbOutbox(new MongoDbConfiguration(Const.Client, Const.DatabaseName)
        {
            Outbox = new MongoDbCollectionConfiguration
            {
                Name = _collectionName
            }
        });

        return outbox.Get();
    }

    public async Task<IEnumerable<Message>> GetAllMessagesAsync()
    {
        var outbox = new MongoDbOutbox(new MongoDbConfiguration(Const.Client, Const.DatabaseName)
        {
            Outbox = new MongoDbCollectionConfiguration
            {
                Name = _collectionName
            }
        });

        return await outbox.GetAsync();
    }
}
