using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperCompletedSweepMetricsTests
{
    [Fact]
    public async Task When_a_sweep_completes_should_record_a_completed_sweep_and_its_duration()
    {
        //Arrange
        using var outbox = new SweepableOutbox();
        using var measurements = new RecordedSweeperMeasurements();
        var sweeper = new TimedOutboxSweeper(
serviceScopeFactory: outbox.ScopeFactory, distributedLock: new InMemoryLock(), options: new TimedOutboxSweeperOptions { TimerInterval = 1, MinimumMessageAge = TimeSpan.Zero }, meter: new SweeperMeter(meterFactory: measurements.MeterFactory, loggerFactory: Initializer.TestLoggerFactory), logger: Microsoft.Extensions.Logging.LoggerFactoryExtensions.CreateLogger<TimedOutboxSweeper>(Initializer.TestLoggerFactory));

        //Act
        await sweeper.StartAsync(CancellationToken.None);
        var recorded = measurements.WaitFor(
            m => m.Of(RecordedSweeperMeasurements.Sweeps).Any() && m.Of(RecordedSweeperMeasurements.SweepDuration).Any(),
            TimeSpan.FromSeconds(3));
        await sweeper.StopAsync(CancellationToken.None);
        sweeper.Dispose();

        //Assert
        Assert.True(recorded, "The sweeper recorded no sweep");
        var sweep = measurements.Of(RecordedSweeperMeasurements.Sweeps).First();
        Assert.Equal(1, sweep.Value);
        Assert.Equal("completed", sweep.Outcome);
        var duration = measurements.Of(RecordedSweeperMeasurements.SweepDuration).First();
        Assert.Equal("completed", duration.Outcome);
        Assert.True(duration.Value >= 0);
    }
}
