using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class When_a_topic_trips_on_the_shared_breaker_should_report_it_until_cooldown_evicts_it_tests
{
    [Test]
    public async Task When_a_topic_trips_on_the_shared_breaker_should_report_it_until_cooldown_evicts_it()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Completed);
        var sharedBreaker = new InMemoryOutboxCircuitBreaker(new OutboxCircuitBreakerOptions { CooldownCount = 2 });
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => sharedBreaker, 3);

        //Act
        sharedBreaker.TripTopic(new RoutingKey("orders"));
        var afterTrip = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        sharedBreaker.CoolDown();
        var afterFirstCoolDown = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        sharedBreaker.CoolDown();
        var afterSecondCoolDown = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        sharedBreaker.CoolDown();
        var afterThirdCoolDown = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        await Assert.That(state.Read().IsActive).IsTrue();
        await Assert.That(afterTrip.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That(afterFirstCoolDown.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That(afterSecondCoolDown.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That(afterThirdCoolDown.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(afterTrip.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
        await Assert.That(afterThirdCoolDown.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
    }
}
