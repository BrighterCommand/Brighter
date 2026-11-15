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

public class When_an_active_sweeper_has_topics_tripped_should_report_degraded_naming_them_tests
{
    [Test]
    public async Task When_an_active_sweeper_has_topics_tripped_should_report_degraded_naming_them()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        for (var i = 0; i < 3; i++)
        {
            state.SweepStarted();
            state.LockObtained();
            state.SweepEnded(SweepOutcome.Completed);
        }
        var breaker = new InMemoryOutboxCircuitBreaker();
        breaker.TripTopic(new RoutingKey("payments"));
        breaker.TripTopic(new RoutingKey("orders"));
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => breaker, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        await Assert.That(state.Read().IsActive).IsTrue();
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That(result.Description).Contains("2");
        await Assert.That(result.Description).Contains("orders");
        await Assert.That(result.Description).Contains("payments");
        await Assert.That(result.Description).Contains("not being swept until their cooldown ends");
        await Assert.That(result.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
        await Assert.That((await Assert.That(result.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEqualTo(new[] { "tripped_topics" });
        var tripped = ((IDictionary<string, object>)(((IDictionary<string, object>)(await Assert.That(result.Data["tripped_topics"]).IsAssignableTo<IDictionary<string, object>>()))));
        await Assert.That((await Assert.That(tripped["count"]).IsTypeOf<int>())).IsEqualTo(2);
        await Assert.That((await Assert.That(tripped["topics"]).IsTypeOf<string[]>())).IsEqualTo(new[] { "orders", "payments" });
    }
}
