using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperEndedSweepStateTests
{
    [Fact]
    public async Task When_a_sweep_completes_should_record_its_outcome_and_no_sweep_in_progress()
    {
        //Arrange
        using var outbox = new SweepableOutbox();

        //Act
        var ended = await SweepOnceAndRead(outbox.ScopeFactory, new InMemoryLock());

        //Assert
        Assert.False(ended.IsSweepInProgress);
        Assert.Equal(TimeSpan.Zero, ended.RunningTime);
        Assert.Equal(SweepOutcome.Completed, ended.LastOutcome);
        Assert.Equal(0, ended.ConsecutiveFailedSweeps);
        Assert.True(ended.IsActive);
    }

    [Fact]
    public async Task When_a_sweep_fails_should_record_its_outcome_and_no_sweep_in_progress()
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddTransient<IAmAnOutboxProducerMediator>(_ => throw new InvalidOperationException("every sweep fails"));
        await using var provider = services.BuildServiceProvider();

        //Act
        var ended = await SweepOnceAndRead(provider.GetRequiredService<IServiceScopeFactory>(), new InMemoryLock());

        //Assert
        Assert.False(ended.IsSweepInProgress);
        Assert.Equal(TimeSpan.Zero, ended.RunningTime);
        Assert.Equal(SweepOutcome.Failed, ended.LastOutcome);
        Assert.Equal(1, ended.ConsecutiveFailedSweeps);
        Assert.True(ended.IsActive);
    }

    [Fact]
    public async Task When_the_sweep_lock_is_held_should_record_its_outcome_and_no_sweep_in_progress()
    {
        //Arrange
        using var outbox = new SweepableOutbox();

        //Act
        var ended = await SweepOnceAndRead(outbox.ScopeFactory, new HeldDistributedLock());

        //Assert
        Assert.False(ended.IsSweepInProgress);
        Assert.Equal(TimeSpan.Zero, ended.RunningTime);
        Assert.Equal(SweepOutcome.LockUnavailable, ended.LastOutcome);
        Assert.False(ended.IsActive);
    }

    private static async Task<OutboxSweeperSnapshot> SweepOnceAndRead(IServiceScopeFactory scopeFactory, IDistributedLock distributedLock)
    {
        var timeProvider = new FakeTimeProvider();
        var state = new OutboxSweeperState(timeProvider);
        using var measurements = new RecordedSweeperMeasurements();
        var sweeper = new TimedOutboxSweeper(
            scopeFactory,
            distributedLock,
            new TimedOutboxSweeperOptions { TimerInterval = 60, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: timeProvider,
            meter: new SweeperMeter(measurements.MeterFactory),
            state: state);

        await sweeper.StartAsync(CancellationToken.None);
        var recorded = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Any(), TimeSpan.FromSeconds(10));
        var ended = state.Read();
        await sweeper.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
        sweeper.Dispose();

        Assert.True(recorded, "The sweeper recorded no sweep");
        return ended;
    }
}
