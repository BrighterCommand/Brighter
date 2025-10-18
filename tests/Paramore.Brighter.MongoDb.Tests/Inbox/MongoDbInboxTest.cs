using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.MongoDb;

namespace Paramore.Brighter.MongoDb.Tests.Inbox;

public class MongoDbInboxTest : InboxTests
{
    private string? _collectionName;
    private MongoDbInbox? _inbox;
    protected override IAmAnInboxSync Inbox => _inbox!;

    protected override void CreateStore()
    {
        _collectionName = $"Inbox{Uuid.New():N}"; 
        _inbox = new MongoDbInbox(new MongoDbConfiguration(Const.Client, Const.DatabaseName)
        {
            Inbox = new MongoDbCollectionConfiguration
            {
                Name = _collectionName
            }
        });
        
        base.CreateStore();
    }

    protected override void DeleteStore()
    {
        var client = Const.Client;
        client.GetDatabase(Const.DatabaseName).DropCollection(_collectionName!);
    }
}
