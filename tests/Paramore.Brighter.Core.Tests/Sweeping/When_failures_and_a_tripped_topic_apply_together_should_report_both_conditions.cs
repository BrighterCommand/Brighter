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

public class When_failures_and_a_tripped_topic_apply_together_should_report_both_conditions_tests
{
    [Fact]
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
        Assert.True(state.Read().IsActive);
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("; ", result.Description);
        var parts = result.Description!.Split("; ");
        Assert.Equal(2, parts.Length);
        Assert.Contains("3", parts[0]);
        Assert.Contains("orders", parts[1]);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), result.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Equal(new[] { "consecutive_failed_sweeps", "tripped_topics" }, Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
        var failures = Assert.IsAssignableFrom<IDictionary<string, object>>(result.Data["consecutive_failed_sweeps"]);
        Assert.Equal(3, Assert.IsType<int>(failures["count"]));
        Assert.Equal(3, Assert.IsType<int>(failures["threshold"]));
        Assert.Equal(10.0, Assert.IsType<double>(failures["timer_interval_seconds"]));
        var tripped = Assert.IsAssignableFrom<IDictionary<string, object>>(result.Data["tripped_topics"]);
        Assert.Equal(1, Assert.IsType<int>(tripped["count"]));
        Assert.Equal(new[] { "orders" }, Assert.IsType<string[]>(tripped["topics"]));
    }
}
