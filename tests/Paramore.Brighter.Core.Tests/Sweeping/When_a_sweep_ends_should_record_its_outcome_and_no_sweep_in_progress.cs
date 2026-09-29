using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperEndedSweepStateTests
{
    [Test]
    public async Task When_a_sweep_completes_should_record_its_outcome_and_no_sweep_in_progress()
    {
        //Arrange
        using var outbox = new SweepableOutbox();

        //Act
        var ended = await SweepOnceAndRead(outbox.ScopeFactory, new InMemoryLock());

        //Assert
        await Assert.That(ended.IsSweepInProgress).IsFalse();
        await Assert.That(ended.RunningTime).IsEqualTo(TimeSpan.Zero);
        await Assert.That(ended.LastOutcome).IsEqualTo(SweepOutcome.Completed);
        await Assert.That(ended.ConsecutiveFailedSweeps).IsEqualTo(0);
        await Assert.That(ended.IsActive).IsTrue();
    }

    [Test]
    public async Task When_a_sweep_fails_should_record_its_outcome_and_no_sweep_in_progress()
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddTransient<IAmAnOutboxProducerMediator>(_ => throw new InvalidOperationException("every sweep fails"));
        await using var provider = services.BuildServiceProvider();

        //Act
        var ended = await SweepOnceAndRead(provider.GetRequiredService<IServiceScopeFactory>(), new InMemoryLock());

        //Assert
        await Assert.That(ended.IsSweepInProgress).IsFalse();
        await Assert.That(ended.RunningTime).IsEqualTo(TimeSpan.Zero);
        await Assert.That(ended.LastOutcome).IsEqualTo(SweepOutcome.Failed);
        await Assert.That(ended.ConsecutiveFailedSweeps).IsEqualTo(1);
        await Assert.That(ended.IsActive).IsTrue();
    }

    [Test]
    public async Task When_the_sweep_lock_is_held_should_record_its_outcome_and_no_sweep_in_progress()
    {
        //Arrange
        using var outbox = new SweepableOutbox();

        //Act
        var ended = await SweepOnceAndRead(outbox.ScopeFactory, new HeldDistributedLock());

        //Assert
        await Assert.That(ended.IsSweepInProgress).IsFalse();
        await Assert.That(ended.RunningTime).IsEqualTo(TimeSpan.Zero);
        await Assert.That(ended.LastOutcome).IsEqualTo(SweepOutcome.LockUnavailable);
        await Assert.That(ended.IsActive).IsFalse();
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

        await Assert.That(recorded).IsTrue().Because("The sweeper recorded no sweep");
        return ended;
    }
}
