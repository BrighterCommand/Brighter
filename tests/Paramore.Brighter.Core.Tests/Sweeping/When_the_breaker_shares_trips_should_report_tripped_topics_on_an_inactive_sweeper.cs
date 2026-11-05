using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class When_the_breaker_shares_trips_should_report_tripped_topics_on_an_inactive_sweeper_tests
{
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = TimeSpan.FromSeconds(50), ClockSkewLeeway = TimeSpan.Zero };

    [Fact]
    public async Task When_the_breaker_shares_trips_should_report_tripped_topics_on_an_inactive_sweeper()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.SweepEnded(SweepOutcome.LockUnavailable);
        var breakerB = await BreakerSeeingTripsFromAnotherInstance();
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => breakerB, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.False(state.Read().IsActive);
        Assert.Equal(HealthStatus.Degraded, result.Status);
        var tripped = Assert.IsAssignableFrom<IDictionary<string, object>>(result.Data["tripped_topics"]);
        Assert.Equal(new[] { "invoices", "orders" }, Assert.IsType<string[]>(tripped["topics"]));
        Assert.Equal(2, tripped["count"]);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), result.Data[RemedyCheckConvention.RemedyKindKey]);
    }

    [Fact]
    public async Task When_the_breaker_shares_trips_should_not_report_tripped_topics_once_the_sweeper_is_stopped()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.SweepEnded(SweepOutcome.LockUnavailable);
        state.Stopped();
        var breakerB = await BreakerSeeingTripsFromAnotherInstance();
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => breakerB, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Empty(Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
    }

    private static async Task<DistributedOutboxCircuitBreaker> BreakerSeeingTripsFromAnotherInstance()
    {
        var clock = new FakeTimeProvider();
        var store = new InMemoryTrippedTopicStore();
        var breakerA = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock);
        var breakerB = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock);
        var batch = breakerA.BeginTripBatch();
        batch.TripTopic(new RoutingKey("orders"));
        batch.TripTopic(new RoutingKey("invoices"));
        await batch.WriteAsync();
        await breakerB.ReadTrippedTopicsAsync();
        return breakerB;
    }
}
