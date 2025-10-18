using System;
using System.Threading.Tasks;

namespace Paramore.Brighter.Base.Test.Locking;

public abstract class RelationalDatabaseDistributedLockingAsyncTest : DistributedLockingAsyncTest
{
    protected abstract string DefaultConnectingString { get; }
    protected RelationalDatabaseConfiguration Configuration { get; private set; } = null!;

    protected override Task BeforeEachTestAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = DefaultConnectingString;
        }
        
        Configuration = new RelationalDatabaseConfiguration(connectionString);
        return base.BeforeEachTestAsync();
    }
}
