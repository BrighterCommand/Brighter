using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperTickLagMetricsTests
{
    [Fact]
    public async Task When_a_sweep_starts_late_should_record_how_late_it_started()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        using var outbox = new SweepableOutbox();
        using var measurements = new RecordedSweeperMeasurements();
        var sweeper = new TimedOutboxSweeper(
            outbox.ScopeFactory,
            new InMemoryLock(),
            new TimedOutboxSweeperOptions { TimerInterval = 5, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: timeProvider,
            meter: new SweeperMeter(measurements.MeterFactory));

        await sweeper.StartAsync(CancellationToken.None);
        var firstSweep = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.TickLag).Count == 1, TimeSpan.FromSeconds(3));

        //Act
        timeProvider.Advance(TimeSpan.FromSeconds(7)); // the next sweep was due at 5 s; the clock is now at 7 s
        var secondSweep = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.TickLag).Count == 2, TimeSpan.FromSeconds(3));
        await sweeper.StopAsync(CancellationToken.None);
        sweeper.Dispose();

        //Assert
        Assert.True(firstSweep, "The sweeper did not record the lag of its first sweep");
        Assert.True(secondSweep, "The sweeper did not record the lag of its second sweep");
        var lags = measurements.Of(RecordedSweeperMeasurements.TickLag);
        Assert.Equal(0, lags[0].Value);
        Assert.Equal(2, lags[1].Value);
    }
}
