using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Locking.MongoDb;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests;

[Trait("Category", "MongoDb")]
public class MongoDbLockingProviderTest
{
    private readonly MongoDbLockingProvider _locking;

    public MongoDbLockingProviderTest()
    {
        _locking = new MongoDbLockingProvider(Configuration.CreateLocking("locking"));
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
