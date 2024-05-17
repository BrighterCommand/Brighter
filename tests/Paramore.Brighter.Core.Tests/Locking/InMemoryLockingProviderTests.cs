using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Locking;

public class InMemoryLockingProviderTests
{
    private IDistributedLock _locking = new InMemoryLock();


    [Fact]
    public async Task GivenAnInMemoryLockingProvider_WhenLockIsCalled_ItCanOnlyBeObtainedOnce()
    {
        var resourceName = $"TestLock-{Guid.NewGuid()}";

        var firstLock = await _locking.ObtainLockAsync(resourceName, CancellationToken.None);
        var secondLock = await _locking.ObtainLockAsync(resourceName, CancellationToken.None); 
            
        Assert.True(firstLock);
        Assert.False(secondLock, "A Lock should not be able to be acquired");
    }
    
    [Fact]
    public async Task GivenAnAzureBlobLockingProviderWithALockedBlob_WhenReleaseLockIsCalled_ItCanOnlyBeLockedAgain()
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
