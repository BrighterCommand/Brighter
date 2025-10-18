using Paramore.Brighter.Base.Test.Locking;
using Paramore.Brighter.Locking.MongoDb;

namespace Paramore.Brighter.MongoDb.Tests.Locking;

public class MongoDbLockingAsyncTest : DistributedLockingAsyncTest
{
    protected override IDistributedLock CreateDistributedLock()
    {
        return new MongoDbLockingProvider(new MongoDbConfiguration(Const.ConnectionString,  Const.DatabaseName)
        {
            Locking = new MongoDbCollectionConfiguration
            {
                Name = "Locking"
            }
        });
    }
}
