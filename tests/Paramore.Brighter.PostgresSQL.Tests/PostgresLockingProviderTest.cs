using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Locking.PostgresSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests;

public class PostgresLockingProviderTest
{
    private readonly PostgresLockingProvider _locking;

    public PostgresLockingProviderTest()
    {
        var helper = new PostgresSqlTestHelper(); 
        helper.SetupDatabase();
        _locking = new PostgresLockingProvider(new PostgresLockingProviderOptions(helper.Configuration.ConnectionString));
    }


    [Fact]
    public async Task GivenAnPostgresLockingProvider_WhenLockIsCalled_ItCanOnlyBeObtainedOnce()
    {
        var resourceName = $"TestLock-{Guid.NewGuid()}";

        var first = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);
        var second = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);

        Assert.True(first);
        Assert.False(second, "A Lock should not be able to be acquired");
    }
    
    
    [Fact]
    public async Task GivenAnPostgresLockingProviderWithALockedBlob_WhenReleaseLockIsCalled_ItCanOnlyBeLockedAgain()
    {
        var resourceName = $"TestLock-{Guid.NewGuid()}";

        var firstLock = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);
        await _locking.ReleaseLockAsync(resourceName, CancellationToken.None);
        var secondLock = await _locking.ObtainLockAsync(resourceName, CancellationToken.None); 
        var thirdLock = await _locking.ObtainLockAsync(resourceName, CancellationToken.None); 
            
        Assert.True(firstLock);
        Assert.True(secondLock, "A Lock should be able to be acquired");
        Assert.False(thirdLock, "A Lock should not be able to be acquired");
    }
}
