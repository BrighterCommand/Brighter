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

public class When_a_tripped_topic_is_tripped_again_should_still_name_it_once_tests
{
    [Fact]
    public async Task When_a_tripped_topic_is_tripped_again_should_still_name_it_once()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Completed);
        var breaker = new InMemoryOutboxCircuitBreaker(new OutboxCircuitBreakerOptions { CooldownCount = 1 });

        //Act
        breaker.TripTopic(new RoutingKey("orders"));
        var first = await BuildCheck(state, breaker).CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        breaker.TripTopic(new RoutingKey("orders"));
        var second = await BuildCheck(state, breaker).CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.True(state.Read().IsActive);
        foreach (var result in new[] { first, second })
        {
            Assert.Equal(HealthStatus.Degraded, result.Status);
            var tripped = Assert.IsAssignableFrom<IDictionary<string, object>>(result.Data["tripped_topics"]);
            Assert.Equal(new[] { "orders" }, Assert.IsType<string[]>(tripped["topics"]));
            Assert.Equal(1, tripped["count"]);
            Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), result.Data[RemedyCheckConvention.RemedyKindKey]);
        }
    }

    private static OutboxSweeperInvestigateHealthCheck BuildCheck(OutboxSweeperState state, InMemoryOutboxCircuitBreaker breaker) =>
        new(state, new TimedOutboxSweeperOptions(), () => breaker, 3);
}
