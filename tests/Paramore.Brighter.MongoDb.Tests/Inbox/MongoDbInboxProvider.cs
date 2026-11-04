using System.Threading.Tasks;
using MongoDB.Driver;
using Const = Paramore.Brighter.MongoDb.Tests.Const;
using MongoDbConfiguration = Paramore.Brighter.MongoDb.MongoDbConfiguration;
using MongoDbCollectionConfiguration = Paramore.Brighter.MongoDb.MongoDbCollectionConfiguration;
using Paramore.Brighter.Inbox.MongoDb;
using Paramore.Brighter.MongoDB.Tests.Inbox.MongoDb.Async;
using Paramore.Brighter.MongoDB.Tests.Inbox.MongoDb.Sync;

namespace Paramore.Brighter.MongoDB.Tests.Inbox.MongoDb;

public class MongoDbInboxProvider : IAmAnInboxProviderSync, IAmAnInboxProviderAsync
{
    private readonly string _collectionName = $"Inbox{Uuid.New():N}";

    public IAmAnInboxSync CreateInbox()
    {
        return new MongoDbInbox(new MongoDbConfiguration(Const.Client, Const.DatabaseName)
        {
            Inbox = new MongoDbCollectionConfiguration
            {
                Name = _collectionName
            }
        });
    }

    public IAmAnInboxAsync CreateInboxAsync()
    {
        return new MongoDbInbox(new MongoDbConfiguration(Const.Client, Const.DatabaseName)
        {
            Inbox = new MongoDbCollectionConfiguration
            {
                Name = _collectionName
            }
        });
    }

    public void CreateStore()
    {
        // The collection is created automatically when the first command is added.
    }

    public Task CreateStoreAsync()
    {
        return Task.CompletedTask;
    }

    public void DeleteStore()
    {
        var database = Const.Client.GetDatabase(Const.DatabaseName);
        database.DropCollection(_collectionName);
    }

    public async Task DeleteStoreAsync()
    {
        var database = Const.Client.GetDatabase(Const.DatabaseName);
        await database.DropCollectionAsync(_collectionName);
    }
}
