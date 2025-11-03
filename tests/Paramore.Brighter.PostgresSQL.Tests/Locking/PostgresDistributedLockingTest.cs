using Paramore.Brighter.Base.Test.Locking;
using Paramore.Brighter.Locking.PostgresSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Locking;

[Collection("Locking")]
public class PostgresDistributedLockingTest : RelationalDatabaseDistributedLockingAsyncTest
{
    protected override IDistributedLock CreateDistributedLock()
    {
        return new PostgresLockingProvider(new PostgresLockingProviderOptions(Configuration.ConnectionString));
    }

    protected override string DefaultConnectingString =>  Const.ConnectionString;
}
