using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Base.Test.Locking;

public abstract class DistributedLockingAsyncTest 
{
    [Before(HookType.Test)]    public async Task Setup()
    {
        await BeforeEachTestAsync();
    }

    [After(HookType.Test)]
    public async Task Cleanup()
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
    
    [Test]
    public async Task When_Obtaining_A_Lock_On_A_Resource_It_Should_Succeed()
    {
        // Arrange
        var resource = Uuid.NewAsString();
        var provider = CreateDistributedLock();
        
        // Act 
        var @lock = await provider.ObtainLockAsync(resource, CancellationToken.None);
        
        // Assert
        await Assert.That(@lock).IsNotNull();
        await Assert.That(@lock).IsNotEmpty();
    }
    
    [Test]
    public async Task When_Trying_To_Obtain_Same_Lock_Twice_With_Same_Instance_It_Should_Fail_Second_Time()
    {
        // Arrange
        var resource = Uuid.NewAsString();
        var provider = CreateDistributedLock();
        
        // Act 
        var lock1 = await provider.ObtainLockAsync(resource, CancellationToken.None);
        var lock2 = await provider.ObtainLockAsync(resource, CancellationToken.None);
        
        // Assert
        await Assert.That(lock1).IsNotNull();
        await Assert.That(lock1).IsNotEmpty();
        await Assert.That(lock2).IsNull();
    }
    
    [Test]
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
        await Assert.That(lock1).IsNotNull();
        await Assert.That(lock1).IsNotEmpty();
        await Assert.That(lock2).IsNull();
    }
    
    [Test]
    public async Task When_Lock_Is_Released_It_Can_Be_Obtained_Again()
    {
        // Arrange
        var resource = Uuid.NewAsString();
        var provider = CreateDistributedLock();
        
        // Act 
        var lock1 = await provider.ObtainLockAsync(resource, CancellationToken.None);
        var lock2 = await provider.ObtainLockAsync(resource, CancellationToken.None);
        
        // Assert - Initial locks
        await Assert.That(lock1).IsNotNull();
        await Assert.That(lock1).IsNotEmpty();
        await Assert.That(lock2).IsNull();
        
        // Act - Release lock
        await provider.ReleaseLockAsync(resource, lock1, CancellationToken.None);

        if (DelayBetweenTryAcquireLockOnSameResource > TimeSpan.Zero)
        {
            await Task.Delay(DelayBetweenTryAcquireLockOnSameResource);
        }
        
        // Assert - Lock can be obtained again
        var lock3 = await provider.ObtainLockAsync(resource, CancellationToken.None);
        await Assert.That(lock3).IsNotNull();
        await Assert.That(lock3).IsNotEmpty();
    }
}
