using System;
using System.Collections.Generic;
using System.Linq;
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

public class When_more_than_ten_topics_are_tripped_should_name_the_first_ten_and_the_total_tests
{
    [Fact]
    public async Task When_more_than_ten_topics_are_tripped_should_name_the_first_ten_and_the_total()
    {
        //Arrange
        var ordinal = new[] { "apple", "banana", "cherry", "damson", "elder", "fig", "grape", "hazel", "iris", "jasmine", "kiwi", "lemon" };
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Completed);
        var breaker = new InMemoryOutboxCircuitBreaker();
        foreach (var name in ordinal.Reverse())
            breaker.TripTopic(new RoutingKey(name));
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => breaker, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.True(state.Read().IsActive);
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), result.Data[RemedyCheckConvention.RemedyKindKey]);
        foreach (var name in ordinal.Take(10))
            Assert.Contains(name, result.Description);
        Assert.DoesNotContain("kiwi", result.Description);
        Assert.DoesNotContain("lemon", result.Description);
        Assert.Contains("2 more (12 in total)", result.Description);
        var tripped = Assert.IsAssignableFrom<IDictionary<string, object>>(result.Data["tripped_topics"]);
        Assert.Equal(12, Assert.IsType<int>(tripped["count"]));
        Assert.Equal(ordinal, Assert.IsType<string[]>(tripped["topics"]));
    }
}
