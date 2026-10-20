using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperStateBlockedInLockAcquisitionTests
{
    [Fact]
    public async Task When_a_sweep_is_blocked_acquiring_the_lock_should_record_it_in_progress_and_not_active()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var state = new OutboxSweeperState(timeProvider);
        using var outbox = new SweepableOutbox();
        using var lockGate = new GatedDistributedLock();
        var sweeper = new TimedOutboxSweeper(
            outbox.ScopeFactory,
            lockGate,
            new TimedOutboxSweeperOptions { TimerInterval = 60, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: timeProvider,
            state: state);

        OutboxSweeperSnapshot blocked;
        try
        {
            await sweeper.StartAsync(CancellationToken.None);
            var reachedLock = lockGate.WaitForSweepToStart(TimeSpan.FromSeconds(10));

            //Act
            timeProvider.Advance(TimeSpan.FromSeconds(40));
            blocked = state.Read();

            //Assert
            Assert.True(reachedLock, "The sweep did not reach the lock");
            Assert.True(blocked.IsSweepInProgress);
            Assert.Equal(TimeSpan.FromSeconds(40), blocked.RunningTime);
            Assert.False(blocked.HoldsLock);
            Assert.False(blocked.IsActive);
        }
        finally
        {
            var stopping = sweeper.StopAsync(CancellationToken.None);
            lockGate.Open();
            await stopping.WaitAsync(TimeSpan.FromSeconds(10));
            sweeper.Dispose();
        }
    }
}
