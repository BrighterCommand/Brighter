using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperLockFailureStateTests
{
    private static readonly TimeSpan s_timerInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan s_guard = TimeSpan.FromSeconds(10);

    [Test]
    public async Task When_obtaining_the_lock_throws_should_count_a_failed_sweep_and_not_be_active()
    {
        //Arrange
        const int sweepsToRun = 3;
        using var outbox = new SweepableOutbox();
        var timeProvider = new FakeTimeProvider();
        var state = new OutboxSweeperState(timeProvider);
        using var measurements = new RecordedSweeperMeasurements();
        var sweeper = new TimedOutboxSweeper(
            outbox.ScopeFactory,
            new ThrowingDistributedLock("the lock store is unreachable"),
            new TimedOutboxSweeperOptions { TimerInterval = (int)s_timerInterval.TotalSeconds, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: timeProvider,
            meter: new SweeperMeter(measurements.MeterFactory),
            state: state);

        //Act
        await sweeper.StartAsync(CancellationToken.None);
        for (var sweep = 1; sweep <= sweepsToRun; sweep++)
        {
            if (sweep > 1)
                timeProvider.Advance(s_timerInterval);

            var recorded = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Count >= sweep, s_guard);
            await Assert.That(recorded).IsTrue().Because($"The sweeper recorded only {measurements.Of(RecordedSweeperMeasurements.Sweeps).Count} of {sweepsToRun} sweeps");
        }

        var ended = state.Read();
        await sweeper.StopAsync(CancellationToken.None).WaitAsync(s_guard);
        sweeper.Dispose();

        //Assert
        await Assert.That(ended.ConsecutiveFailedSweeps).IsEqualTo(sweepsToRun);
        await Assert.That(ended.LastOutcome).IsEqualTo(SweepOutcome.Failed);
        await Assert.That(ended.IsActive).IsFalse();
    }
}
