using Paramore.Brighter.Base.Test.Locking;
using Paramore.Brighter.Locking.Oracle;

namespace Paramore.Brighter.Oracle.Tests.Locking;

public class OracleDistributedLockingTest : RelationalDatabaseDistributedLockingAsyncTest
{
    protected override string DefaultConnectingString => Const.DefaultConnectingString;

    protected override IDistributedLock CreateDistributedLock()
    {
        return new OracleLockingProvider(new OracleConnectionProvider(Configuration));
    }
}
