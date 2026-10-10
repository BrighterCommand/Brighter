using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperLockUnavailableMetricsTests
{
    [Fact]
    public async Task When_the_sweep_lock_is_held_should_record_a_lock_unavailable_sweep()
    {
        //Arrange
        using var outbox = new SweepableOutbox();
        using var measurements = new RecordedSweeperMeasurements();
        var sweeper = new TimedOutboxSweeper(
            outbox.ScopeFactory,
            new HeldDistributedLock(),
            new TimedOutboxSweeperOptions { TimerInterval = 1, MinimumMessageAge = TimeSpan.Zero },
            meter: new SweeperMeter(measurements.MeterFactory));

        //Act
        await sweeper.StartAsync(CancellationToken.None);
        var recorded = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Any(), TimeSpan.FromSeconds(3));
        await sweeper.StopAsync(CancellationToken.None);
        sweeper.Dispose();

        //Assert
        Assert.True(recorded, "The sweeper recorded no sweep");
        Assert.Equal("lock_unavailable", measurements.Of(RecordedSweeperMeasurements.Sweeps).First().Outcome);
    }
}
