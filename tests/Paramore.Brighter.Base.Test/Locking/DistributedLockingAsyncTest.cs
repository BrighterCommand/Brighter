using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Base.Test.Locking;

public abstract class DistributedLockingAsyncTest
{
    protected DistributedLockingAsyncTest()
    {
        // ReSharper disable once VirtualMemberCallInConstructor
        BeforeEachTestAsync().GetAwaiter().GetResult();
    }

    protected virtual Task BeforeEachTestAsync()
    {
        return Task.CompletedTask;
    }
    
    
    public void Dispose()
    {
        AfterEachTestAsync().GetAwaiter().GetResult();
    }

    protected abstract IDistributedLock CreateDistributedLock();

    protected virtual Task AfterEachTestAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task LockCanBeObtained()
    {
        // Arrange
        var resource = Uuid.NewAsString();
        var provider = CreateDistributedLock();
        
        // Act 
        var @lock = await provider.ObtainLockAsync(resource, CancellationToken.None);
        
        // Assert
        Assert.NotNull(@lock);
        Assert.NotEmpty(@lock);
    }
    
    [Fact]
    public async Task TryToObtainLockTwiceWithSameInstance()
    {
        // Arrange
        var resource = Uuid.NewAsString();
        var provider = CreateDistributedLock();
        
        // Act 
        var lock1 = await provider.ObtainLockAsync(resource, CancellationToken.None);
        var lock2 = await provider.ObtainLockAsync(resource, CancellationToken.None);
        
        // Assert
        Assert.NotNull(lock1);
        Assert.NotEmpty(lock1);
        Assert.Null(lock2);
    }
    
    [Fact]
    public async Task TryToObtainLockTwiceWithDifferentInstance()
    {
        // Arrange
        var resource = Uuid.NewAsString();
        var provider1 = CreateDistributedLock();
        var provider2 = CreateDistributedLock();
        
        // Act 
        var lock1 = await provider1.ObtainLockAsync(resource, CancellationToken.None);
        var lock2 = await provider2.ObtainLockAsync(resource, CancellationToken.None);
        
        // Assert
        Assert.NotNull(lock1);
        Assert.NotEmpty(lock1);
        Assert.Null(lock2);
    }
    
    [Fact]
    public async Task FailsToObtainLockUntilThePreviousLockIsReleased()
    {
        // Arrange
        var resource = Uuid.NewAsString();
        var provider = CreateDistributedLock();
        
        // Act 
        var lock1 = await provider.ObtainLockAsync(resource, CancellationToken.None);
        var lock2 = await provider.ObtainLockAsync(resource, CancellationToken.None);
        
        Assert.NotNull(lock1);
        Assert.NotEmpty(lock1);
        Assert.Null(lock2);
        
        await provider.ReleaseLockAsync(resource, lock1, CancellationToken.None);
        
        // Assert
        var lock3 = await provider.ObtainLockAsync(resource, CancellationToken.None);
        Assert.NotNull(lock3);
        Assert.NotEmpty(lock3);
    }
}
