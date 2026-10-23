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

public class When_a_topic_trips_on_the_shared_breaker_should_report_it_until_cooldown_evicts_it_tests
{
    [Fact]
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
        Assert.True(state.Read().IsActive);
        Assert.Equal(HealthStatus.Degraded, afterTrip.Status);
        Assert.Equal(HealthStatus.Degraded, afterFirstCoolDown.Status);
        Assert.Equal(HealthStatus.Degraded, afterSecondCoolDown.Status);
        Assert.Equal(HealthStatus.Healthy, afterThirdCoolDown.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), afterTrip.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), afterThirdCoolDown.Data[RemedyCheckConvention.RemedyKindKey]);
    }
}
