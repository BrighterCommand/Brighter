using Paramore.Brighter.Base.Test.Locking;
using Paramore.Brighter.Locking.PostgresSql;

namespace Paramore.Brighter.PostgresSQL.Tests.Locking;

public class PostgresDistributedLockingTest : RelationalDatabaseDistributedLockingAsyncTest
{
    protected override IDistributedLock CreateDistributedLock()
    {
        return new PostgresLockingProvider(new PostgresLockingProviderOptions(Configuration.ConnectionString));
    }

    protected override string DefaultConnectingString =>  Const.ConnectionString;
}
