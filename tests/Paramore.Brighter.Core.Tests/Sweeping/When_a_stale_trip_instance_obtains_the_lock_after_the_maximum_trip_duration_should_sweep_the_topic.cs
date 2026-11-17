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

public class SweeperStaleTripTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task When_a_stale_trip_instance_obtains_the_lock_after_the_maximum_trip_duration_should_sweep_the_topic()
    {
        //Arrange
        var clock = new FakeTimeProvider();
        var breaker = new InMemoryOutboxCircuitBreaker(
            new OutboxCircuitBreakerOptions { MaximumTripDuration = TimeSpan.FromMinutes(5) },
            timeProvider: clock);
        using var outbox = new SweepableOutbox(breaker);
        using var measurements = new RecordedSweeperMeasurements();
        var distributedLock = new SwitchableDistributedLock();
        distributedLock.MakeUnavailable();
        outbox.AddUndispatched();
        breaker.TripTopic(outbox.RoutingKey);

        var sweeper = new TimedOutboxSweeper(
            outbox.ScopeFactory,
            distributedLock,
            new TimedOutboxSweeperOptions { TimerInterval = 5, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: clock,
            meter: new SweeperMeter(measurements.MeterFactory));

        //Act
        await sweeper.StartAsync(CancellationToken.None);
        var withheldAtStart = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Count == 1, s_timeout);

        clock.Advance(TimeSpan.FromMinutes(6));
        var withheldAfterD = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Count == 2, s_timeout);
        var publishedWhileWithheld = outbox.Bus.Stream(outbox.RoutingKey).Any();

        distributedLock.Grant();
        clock.Advance(TimeSpan.FromSeconds(5));
        var sweptWithLock = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Count == 3, s_timeout);
        var publishedAfterLock = outbox.Bus.Stream(outbox.RoutingKey).Any();

        await sweeper.StopAsync(CancellationToken.None);
        sweeper.Dispose();

        //Assert
        Assert.True(withheldAtStart && withheldAfterD && sweptWithLock, "The sweeps did not all complete");
        Assert.False(publishedWhileWithheld);
        Assert.True(publishedAfterLock, "The topic was still excluded from the sweep that obtained the lock");
    }
}
