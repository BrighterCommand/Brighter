using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweepsWithDefaultCircuitBreakerOptionsTests
{
    private const int DEFAULT_COOLDOWN_COUNT = 10;
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task When_sweeps_run_on_schedule_with_default_options_should_exclude_a_tripped_topic_from_exactly_ten_sweeps()
    {
        //Arrange
        var clock = new FakeTimeProvider();
        var breaker = new InMemoryOutboxCircuitBreaker(timeProvider: clock);
        using var outbox = new SweepableOutbox(breaker);
        using var measurements = new RecordedSweeperMeasurements();
        outbox.AddUndispatched();
        breaker.TripTopic(outbox.RoutingKey);

        var sweeper = new TimedOutboxSweeper(
            outbox.ScopeFactory,
            new InMemoryLock(),
            new TimedOutboxSweeperOptions { TimerInterval = 5, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: clock,
            meter: new SweeperMeter(measurements.MeterFactory));

        //Act
        await sweeper.StartAsync(CancellationToken.None);
        var publishedAfterSweep = new bool[DEFAULT_COOLDOWN_COUNT + 1];
        for (var sweep = 1; sweep <= DEFAULT_COOLDOWN_COUNT + 1; sweep++)
        {
            if (sweep > 1)
                clock.Advance(TimeSpan.FromSeconds(5));
            var swept = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Count == sweep, s_timeout);
            Assert.True(swept, $"Sweep {sweep} did not complete");
            publishedAfterSweep[sweep - 1] = outbox.Bus.Stream(outbox.RoutingKey).Any();
        }
        await sweeper.StopAsync(CancellationToken.None);
        sweeper.Dispose();

        //Assert
        Assert.All(publishedAfterSweep.Take(DEFAULT_COOLDOWN_COUNT), published => Assert.False(published));
        Assert.True(publishedAfterSweep[DEFAULT_COOLDOWN_COUNT]);
    }
}
