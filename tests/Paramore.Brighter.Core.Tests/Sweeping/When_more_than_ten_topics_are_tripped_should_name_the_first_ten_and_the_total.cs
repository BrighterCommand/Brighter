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

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class When_more_than_ten_topics_are_tripped_should_name_the_first_ten_and_the_total_tests
{
    [Test]
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
        await Assert.That(state.Read().IsActive).IsTrue();
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That(result.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
        foreach (var name in ordinal.Take(10))
            await Assert.That(result.Description).Contains(name);
        await Assert.That(result.Description).DoesNotContain("kiwi");
        await Assert.That(result.Description).DoesNotContain("lemon");
        await Assert.That(result.Description).Contains("2 more (12 in total)");
        var tripped = ((IDictionary<string, object>)(((IDictionary<string, object>)(await Assert.That(result.Data["tripped_topics"]).IsAssignableTo<IDictionary<string, object>>()))));
        await Assert.That((await Assert.That(tripped["count"]).IsTypeOf<int>())).IsEqualTo(12);
        await Assert.That((await Assert.That(tripped["topics"]).IsTypeOf<string[]>())).IsEqualTo(ordinal);
    }
}
