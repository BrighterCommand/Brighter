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

public class When_the_circuit_breaker_stops_listing_a_topic_should_stop_naming_it_tests
{
    [Fact]
    public async Task When_the_circuit_breaker_stops_listing_a_topic_should_stop_naming_it()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Completed);
        var breaker = new InMemoryOutboxCircuitBreaker(new OutboxCircuitBreakerOptions { CooldownCount = 1 });
        breaker.TripTopic(new RoutingKey("payments"));
        breaker.CoolDown();
        breaker.TripTopic(new RoutingKey("orders"));
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => breaker, 3);

        //Act
        var bothTripped = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        breaker.CoolDown();
        var onlyOrders = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        breaker.CoolDown();
        var noneTripped = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.True(state.Read().IsActive);
        Assert.Equal(HealthStatus.Degraded, bothTripped.Status);
        Assert.Equal(new[] { "orders", "payments" }, TrippedNames(bothTripped));
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), bothTripped.Data[RemedyCheckConvention.RemedyKindKey]);

        Assert.Equal(HealthStatus.Degraded, onlyOrders.Status);
        Assert.Equal(new[] { "orders" }, TrippedNames(onlyOrders));
        Assert.DoesNotContain("payments", onlyOrders.Description);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), onlyOrders.Data[RemedyCheckConvention.RemedyKindKey]);

        Assert.Equal(HealthStatus.Healthy, noneTripped.Status);
        Assert.Empty(Assert.IsType<string[]>(noneTripped.Data[RemedyCheckConvention.ConditionsKey]));
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), noneTripped.Data[RemedyCheckConvention.RemedyKindKey]);
    }

    private static string[] TrippedNames(HealthCheckResult result)
    {
        var tripped = Assert.IsAssignableFrom<IDictionary<string, object>>(result.Data["tripped_topics"]);
        return Assert.IsType<string[]>(tripped["topics"]);
    }
}
