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
        _locking = new PostgresLockingProvider(
            new PostgresLockingProviderOptions(helper.Configuration.ConnectionString)
        );
    }

    [Fact]
    public async Task GivenAnPostgresLockingProvider_WhenLockIsCalled_ItCanOnlyBeObtainedOnce()
    {
        var resourceName = $"TestLock-{Guid.NewGuid()}";

        var first = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);
        var second = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);

        Assert.Equal(first, resourceName);
        Assert.Null(second);
    }

    [Fact]
    public async Task GivenAnPostgresLockingProviderWithALockedBlob_WhenReleaseLockIsCalled_ItCanOnlyBeLockedAgain()
    {
        var resourceName = $"TestLock-{Guid.NewGuid()}";

        var first = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);
        await _locking.ReleaseLockAsync(resourceName, first, CancellationToken.None);

        var second = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);
        var third = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);

        Assert.Equal(first, resourceName);
        Assert.Equal(second, resourceName);
        Assert.Null(third);
    }
}
