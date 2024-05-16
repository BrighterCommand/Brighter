using Azure.Identity;
using Paramore.Brighter.Locking.Azure;

namespace Paramore.Brighter.Azure.Tests;

public class AzureBlobLockingProviderTests
{
    private IDistributedLock _blobLocking;

    public AzureBlobLockingProviderTests()
    {
        var options = new AzureBlobLockingProviderOptions(
            new Uri("https://brighterarchivertest.blob.core.windows.net/locking"), new AzureCliCredential());
        
        _blobLocking = new AzureBlobLockingProvider(options);
    }

    [Test]
    public async Task GivenAnAzureBlobLockingProvider_WhenLockIsCalled_ItCanOnlyBeObtainedOnce()
    {
        var resourceName = $"TestLock-{Guid.NewGuid()}";

        var firstLock = await _blobLocking.ObtainLockAsync(resourceName, CancellationToken.None);
        var secondLock = await _blobLocking.ObtainLockAsync(resourceName, CancellationToken.None); 
            
        Assert.That(firstLock, Is.True);
        Assert.That(secondLock, Is.False, "A Lock should not be able to be acquired");
    }
    
    [Test]
    public async Task GivenAnAzureBlobLockingProviderWithALockedBlob_WhenReleaseLockIsCalled_ItCanOnlyBeLockedAgain()
    {
        var resourceName = $"TestLock-{Guid.NewGuid()}";

        var firstLock = await _blobLocking.ObtainLockAsync(resourceName, CancellationToken.None);
        await _blobLocking.ReleaseLockAsync(resourceName, CancellationToken.None);
        var secondLock = await _blobLocking.ObtainLockAsync(resourceName, CancellationToken.None); 
        var thirdLock = await _blobLocking.ObtainLockAsync(resourceName, CancellationToken.None); 
            
        Assert.That(firstLock, Is.True);
        Assert.That(secondLock, Is.True, "A Lock should be able to be acquired");
        Assert.That(thirdLock, Is.False, "A Lock should not be able to be acquired");
    }
    
}
