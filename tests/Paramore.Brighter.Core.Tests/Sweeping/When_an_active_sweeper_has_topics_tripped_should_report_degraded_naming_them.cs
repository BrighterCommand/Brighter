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

public class When_an_active_sweeper_has_topics_tripped_should_report_degraded_naming_them_tests
{
    [Fact]
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
        Assert.True(state.Read().IsActive);
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("2", result.Description);
        Assert.Contains("orders", result.Description);
        Assert.Contains("payments", result.Description);
        Assert.Contains("not being swept until their cooldown ends", result.Description);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), result.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Equal(new[] { "tripped_topics" }, Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
        var tripped = Assert.IsAssignableFrom<IDictionary<string, object>>(result.Data["tripped_topics"]);
        Assert.Equal(2, Assert.IsType<int>(tripped["count"]));
        Assert.Equal(new[] { "orders", "payments" }, Assert.IsType<string[]>(tripped["topics"]));
    }
}
