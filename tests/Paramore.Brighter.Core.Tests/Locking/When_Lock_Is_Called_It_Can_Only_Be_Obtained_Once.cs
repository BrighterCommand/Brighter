using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Locking;
public class InMemoryLockingProviderTests
{
    private IDistributedLock _locking = new InMemoryLock();
    [Test]
    public async Task WhenLockIsCalled_ItCanOnlyBeObtainedOnce()
    {
        var resourceName = $"TestLock-{Guid.NewGuid()}";
        var firstLock = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);
        var secondLock = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);
        await Assert.That(firstLock).IsNotNull();
        await Assert.That(secondLock).IsNull();
    }

    [Test]
    public async Task GivenAnInMemoryLockingProvider_WhenReleaseLockIsCalled_ItCanOnlyBeLockedAgain()
    {
        var resourceName = $"TestLock-{Guid.NewGuid()}";
        var firstLock = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);
        await _locking.ReleaseLockAsync(resourceName, firstLock, CancellationToken.None);
        var secondLock = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);
        var thirdLock = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);
        await Assert.That(firstLock).IsNotNull();
        await Assert.That(secondLock).IsNotNull();
        await Assert.That(thirdLock).IsNull();
    }
}