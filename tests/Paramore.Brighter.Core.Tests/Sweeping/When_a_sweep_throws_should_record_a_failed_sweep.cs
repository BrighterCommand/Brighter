using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperFailedSweepMetricsTests
{
    [Fact]
    public async Task When_a_sweep_throws_should_record_a_failed_sweep()
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddTransient<IAmAnOutboxProducerMediator>(_ => throw new InvalidOperationException("every sweep fails"));
        await using var provider = services.BuildServiceProvider();

        using var measurements = new RecordedSweeperMeasurements();
        var sweeper = new TimedOutboxSweeper(
serviceScopeFactory: provider.GetRequiredService<IServiceScopeFactory>(), distributedLock: new InMemoryLock(), options: new TimedOutboxSweeperOptions { TimerInterval = 1, MinimumMessageAge = TimeSpan.Zero }, meter: new SweeperMeter(meterFactory: measurements.MeterFactory, loggerFactory: Initializer.TestLoggerFactory), logger: Microsoft.Extensions.Logging.LoggerFactoryExtensions.CreateLogger<TimedOutboxSweeper>(Initializer.TestLoggerFactory));

        //Act
        await sweeper.StartAsync(CancellationToken.None);
        var recorded = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Any(), TimeSpan.FromSeconds(3));
        await sweeper.StopAsync(CancellationToken.None);
        sweeper.Dispose();

        //Assert
        Assert.True(recorded, "The sweeper recorded no sweep");
        Assert.Equal("failed", measurements.Of(RecordedSweeperMeasurements.Sweeps).First().Outcome);
    }
}
