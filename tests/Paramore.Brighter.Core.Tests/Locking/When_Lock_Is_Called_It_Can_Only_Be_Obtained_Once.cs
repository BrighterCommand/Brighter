using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Locking;

public class InMemoryLockingProviderTests
{
    private IDistributedLock _locking = new InMemoryLock();


    [Fact]
    public async Task WhenLockIsCalled_ItCanOnlyBeObtainedOnce()
    {
        var resourceName = $"TestLock-{Guid.NewGuid()}";

        var firstLock = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);
        var secondLock = await _locking.ObtainLockAsync(resourceName, CancellationToken.None); 
            
        Assert.NotNull(firstLock);
        Assert.Null(secondLock);
    }
    
    [Fact]
    public async Task GivenAnInMemoryLockingProvider_WhenReleaseLockIsCalled_ItCanOnlyBeLockedAgain()
    {
        var resourceName = $"TestLock-{Guid.NewGuid()}";

        var firstLock = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);
        await _locking.ReleaseLockAsync(resourceName, firstLock, CancellationToken.None);
        var secondLock = await _locking.ObtainLockAsync(resourceName, CancellationToken.None); 
        var thirdLock = await _locking.ObtainLockAsync(resourceName, CancellationToken.None); 
            
        Assert.NotNull(firstLock);
        Assert.NotNull(secondLock);
        Assert.Null(thirdLock);
    }
}
