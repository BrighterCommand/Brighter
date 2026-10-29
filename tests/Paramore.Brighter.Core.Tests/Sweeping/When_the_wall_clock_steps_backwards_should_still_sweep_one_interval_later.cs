using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class TimedOutboxSweeperWallClockTests
{
    [Fact]
    public async Task When_the_wall_clock_steps_backwards_should_still_sweep_one_interval_later()
    {
        //Arrange
        var clock = new SteppableWallClock();
        using var outbox = new SweepableOutbox();
        using var measurements = new RecordedSweeperMeasurements();
        var sweeper = new TimedOutboxSweeper(
            outbox.ScopeFactory,
            new InMemoryLock(),
            new TimedOutboxSweeperOptions { TimerInterval = 5, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: clock,
            meter: new SweeperMeter(measurements.MeterFactory));

        await sweeper.StartAsync(CancellationToken.None);
        var firstSweep = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Count == 1, TimeSpan.FromSeconds(3));

        //Act
        clock.StepWallClockBack(TimeSpan.FromHours(1)); // e.g. an NTP correction
        clock.Advance(TimeSpan.FromSeconds(5)); // one interval of elapsed time
        var secondSweep = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Count == 2, TimeSpan.FromSeconds(3));
        await sweeper.StopAsync(CancellationToken.None);
        sweeper.Dispose();

        //Assert
        Assert.True(firstSweep, "The sweeper did not sweep at start");
        Assert.True(secondSweep, "The sweeper did not sweep one interval later after the wall clock stepped back an hour");
    }
}
