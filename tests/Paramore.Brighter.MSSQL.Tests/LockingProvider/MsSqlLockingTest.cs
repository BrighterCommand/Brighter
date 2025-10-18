using Paramore.Brighter.Base.Test.Locking;
using Paramore.Brighter.Locking.MsSql;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.LockingProvider;

public class MsSqlLockingTest : RelationalDatabaseDistributedLockingAsyncTest
{
    protected override string DefaultConnectingString => Tests.Configuration.DefaultConnectingString;
    protected override IDistributedLock CreateDistributedLock()
    {
        Tests.Configuration.EnsureDatabaseExists(Configuration.ConnectionString);
        return new MsSqlLockingProvider(new MsSqlConnectionProvider(Configuration));
    }
}
