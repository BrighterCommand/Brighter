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

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class When_the_circuit_breaker_stops_listing_a_topic_should_stop_naming_it_tests
{
    [Test]
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
        await Assert.That(state.Read().IsActive).IsTrue();
        await Assert.That(bothTripped.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That(TrippedNames(bothTripped)).IsEqualTo(new[] { "orders", "payments" });
        await Assert.That(bothTripped.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));

        await Assert.That(onlyOrders.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That(TrippedNames(onlyOrders)).IsEqualTo(new[] { "orders" });
        await Assert.That(onlyOrders.Description).DoesNotContain("payments");
        await Assert.That(onlyOrders.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));

        await Assert.That(noneTripped.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That((await Assert.That(noneTripped.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEmpty();
        await Assert.That(noneTripped.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
    }

    private static async Task<string[]> TrippedNames(HealthCheckResult result)
    {
        var tripped = ((IDictionary<string, object>)(((IDictionary<string, object>)(await Assert.That(result.Data["tripped_topics"]).IsAssignableTo<IDictionary<string, object>>()))));
        return await Assert.That(tripped["topics"]).IsTypeOf<string[]>();
    }
}
