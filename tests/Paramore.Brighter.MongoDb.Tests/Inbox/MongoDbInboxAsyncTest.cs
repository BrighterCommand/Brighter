using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.MongoDb;

namespace Paramore.Brighter.MongoDb.Tests.Inbox;

public class MongoDbInboxAsyncTest : InboxAsyncTest
{
    private string? _collectionName;
    private MongoDbInbox? _inbox;
    protected override IAmAnInboxAsync Inbox => _inbox!;

    protected override Task CreateStoreAsync()
    {
        _collectionName = $"Inbox{Uuid.New():N}"; 
        _inbox = new MongoDbInbox(new MongoDbConfiguration(Const.Client, Const.DatabaseName)
        {
            Inbox = new MongoDbCollectionConfiguration
            {
                Name = _collectionName
            }
        });
        
        return base.CreateStoreAsync();
    }

    protected override async Task DeleteStoreAsync()
    {
        var client = Const.Client;
        await client.GetDatabase(Const.DatabaseName).DropCollectionAsync(_collectionName!);
    }
}
