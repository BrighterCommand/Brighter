using Paramore.Brighter.Base.Test.Locking;
using Paramore.Brighter.Locking.MsSql;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.LockingProvider;

public class MySqlLockingTest : RelationalDatabaseDistributedLockingAsyncTest
{
    protected override string DefaultConnectingString => Const.DefaultConnectingString;
    protected override IDistributedLock CreateDistributedLock()
    {
        return new MsSqlLockingProvider(new MsSqlConnectionProvider(Configuration));
    }
}
