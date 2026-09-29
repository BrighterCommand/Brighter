using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Base.Test.Locking;
using Paramore.Brighter.Locking.MySql;
using Paramore.Brighter.MySql;

namespace Paramore.Brighter.MySQL.Tests.Locking;

public class MySqlLockingTest : RelationalDatabaseDistributedLockingAsyncTest
{
    protected override string DefaultConnectingString => Const.DefaultConnectingString;
    protected override IDistributedLock CreateDistributedLock()
    {
        return new MySqlLockingProvider(new MySqlConnectionProvider(Configuration), loggerFactory: NullLoggerFactory.Instance);
    }
}
