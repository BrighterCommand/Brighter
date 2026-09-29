using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperFailedSweepMetricsTests
{
    [Test]
    public async Task When_a_sweep_throws_should_record_a_failed_sweep()
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddTransient<IAmAnOutboxProducerMediator>(_ => throw new InvalidOperationException("every sweep fails"));
        await using var provider = services.BuildServiceProvider();

        using var measurements = new RecordedSweeperMeasurements();
        var sweeper = new TimedOutboxSweeper(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new InMemoryLock(),
            new TimedOutboxSweeperOptions { TimerInterval = 1, MinimumMessageAge = TimeSpan.Zero },
            meter: new SweeperMeter(measurements.MeterFactory));

        //Act
        await sweeper.StartAsync(CancellationToken.None);
        var recorded = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Any(), TimeSpan.FromSeconds(3));
        await sweeper.StopAsync(CancellationToken.None);
        sweeper.Dispose();

        //Assert
        await Assert.That(recorded).IsTrue().Because("The sweeper recorded no sweep");
        await Assert.That(measurements.Of(RecordedSweeperMeasurements.Sweeps).First().Outcome).IsEqualTo("failed");
    }
}
