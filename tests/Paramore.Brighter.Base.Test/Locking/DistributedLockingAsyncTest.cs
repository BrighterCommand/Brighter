using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Base.Test.Locking;

public abstract class DistributedLockingAsyncTest : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await BeforeEachTestAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await AfterEachTestAsync();
    }
    

    protected virtual Task BeforeEachTestAsync()
    {
        return Task.CompletedTask;
    }
    
    protected abstract IDistributedLock CreateDistributedLock();

    protected virtual Task AfterEachTestAsync()
    {
        return Task.CompletedTask;
    }

    protected virtual TimeSpan DelayBetweenTryAcquireLockOnSameResource { get; } = TimeSpan.Zero;
    
    [Fact]
    public async Task When_Obtaining_A_Lock_On_A_Resource_It_Should_Succeed()
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
    public async Task When_Trying_To_Obtain_Same_Lock_Twice_With_Same_Instance_It_Should_Fail_Second_Time()
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
    public async Task When_Trying_To_Obtain_Same_Lock_With_Different_Instances_It_Should_Fail_Second_Time()
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
    public async Task When_Lock_Is_Released_It_Can_Be_Obtained_Again()
    {
        // Arrange
        var resource = Uuid.NewAsString();
        var provider = CreateDistributedLock();
        
        // Act 
        var lock1 = await provider.ObtainLockAsync(resource, CancellationToken.None);
        var lock2 = await provider.ObtainLockAsync(resource, CancellationToken.None);
        
        // Assert - Initial locks
        Assert.NotNull(lock1);
        Assert.NotEmpty(lock1);
        Assert.Null(lock2);
        
        // Act - Release lock
        await provider.ReleaseLockAsync(resource, lock1, CancellationToken.None);

        if (DelayBetweenTryAcquireLockOnSameResource > TimeSpan.Zero)
        {
            await Task.Delay(DelayBetweenTryAcquireLockOnSameResource);
        }
        
        // Assert - Lock can be obtained again
        var lock3 = await provider.ObtainLockAsync(resource, CancellationToken.None);
        Assert.NotNull(lock3);
        Assert.NotEmpty(lock3);
    }
}
