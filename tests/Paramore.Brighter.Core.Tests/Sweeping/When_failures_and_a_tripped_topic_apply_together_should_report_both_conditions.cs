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

public class When_failures_and_a_tripped_topic_apply_together_should_report_both_conditions_tests
{
    [Test]
    public async Task When_failures_and_a_tripped_topic_apply_together_should_report_both_conditions()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        for (var i = 0; i < 3; i++)
        {
            state.SweepStarted();
            state.LockObtained();
            state.SweepEnded(SweepOutcome.Failed);
        }
        var breaker = new InMemoryOutboxCircuitBreaker();
        breaker.TripTopic(new RoutingKey("orders"));
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions { TimerInterval = 10 }, () => breaker, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        await Assert.That(state.Read().IsActive).IsTrue();
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That(result.Description).Contains("; ");
        var parts = result.Description!.Split("; ");
        await Assert.That(parts.Length).IsEqualTo(2);
        await Assert.That(parts[0]).Contains("3");
        await Assert.That(parts[1]).Contains("orders");
        await Assert.That(result.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
        await Assert.That((await Assert.That(result.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEqualTo(new[] { "consecutive_failed_sweeps", "tripped_topics" });
        var failures = ((IDictionary<string, object>)(((IDictionary<string, object>)(await Assert.That(result.Data["consecutive_failed_sweeps"]).IsAssignableTo<IDictionary<string, object>>()))));
        await Assert.That((await Assert.That(failures["count"]).IsTypeOf<int>())).IsEqualTo(3);
        await Assert.That((await Assert.That(failures["threshold"]).IsTypeOf<int>())).IsEqualTo(3);
        await Assert.That((await Assert.That(failures["timer_interval_seconds"]).IsTypeOf<double>())).IsEqualTo(10.0);
        var tripped = ((IDictionary<string, object>)(((IDictionary<string, object>)(await Assert.That(result.Data["tripped_topics"]).IsAssignableTo<IDictionary<string, object>>()))));
        await Assert.That((await Assert.That(tripped["count"]).IsTypeOf<int>())).IsEqualTo(1);
        await Assert.That((await Assert.That(tripped["topics"]).IsTypeOf<string[]>())).IsEqualTo(new[] { "orders" });
    }
}
