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

public class When_a_tripped_topic_is_tripped_again_should_still_name_it_once_tests
{
    [Test]
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
        await Assert.That(state.Read().IsActive).IsTrue();
        foreach (var result in new[] { first, second })
        {
            await Assert.That(result.Status).IsEqualTo(HealthStatus.Degraded);
            var tripped = ((IDictionary<string, object>)(((IDictionary<string, object>)(await Assert.That(result.Data["tripped_topics"]).IsAssignableTo<IDictionary<string, object>>()))));
            await Assert.That((await Assert.That(tripped["topics"]).IsTypeOf<string[]>())).IsEqualTo(new[] { "orders" });
            await Assert.That(tripped["count"]).IsEqualTo(1);
            await Assert.That(result.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
        }
    }

    private static OutboxSweeperInvestigateHealthCheck BuildCheck(OutboxSweeperState state, InMemoryOutboxCircuitBreaker breaker) =>
        new(state, new TimedOutboxSweeperOptions(), () => breaker, 3);
}
