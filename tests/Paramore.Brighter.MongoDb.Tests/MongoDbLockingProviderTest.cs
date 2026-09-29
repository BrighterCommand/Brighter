using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Locking.MongoDb;

namespace Paramore.Brighter.MongoDb.Tests;

[Category("MongoDb")]
public class MongoDbLockingProviderTest
{
    private readonly MongoDbLockingProvider _locking;

    public MongoDbLockingProviderTest()
    {
        _locking = new MongoDbLockingProvider(Configuration.CreateLocking("locking"));
    }

    [Test]
    public async Task GivenAnPostgresLockingProvider_WhenLockIsCalled_ItCanOnlyBeObtainedOnce()
    {
        var resourceName = $"TestLock-{Guid.NewGuid()}";

        var first = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);
        var second = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);

        await Assert.That(resourceName).IsEqualTo(first);
        await Assert.That(second).IsNull();
    }

    [Test]
    public async Task GivenAnPostgresLockingProviderWithALockedBlob_WhenReleaseLockIsCalled_ItCanOnlyBeLockedAgain()
    {
        var resourceName = $"TestLock-{Guid.NewGuid()}";

        var first = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);
        await _locking.ReleaseLockAsync(resourceName, first, CancellationToken.None);

        var second = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);
        var third = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);

        await Assert.That(resourceName).IsEqualTo(first);
        await Assert.That(resourceName).IsEqualTo(second);
        await Assert.That(third).IsNull();
    }
}
