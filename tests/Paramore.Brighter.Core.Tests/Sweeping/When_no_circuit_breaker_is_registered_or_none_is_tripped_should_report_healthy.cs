using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class When_no_circuit_breaker_is_registered_or_none_is_tripped_should_report_healthy_tests
{
    [Test]
    public async Task When_no_lookup_delegate_is_registered_should_report_healthy()
    {
        //Arrange
        var state = ActiveState();
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), null, 3);

        //Act
        HealthCheckResult result = default;
        var exception = await TestExceptionRecorder.CaptureAsync(async () => result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None));

        //Assert
        await Assert.That(exception).IsNull();
        await AssertHealthyWithNoConditions(state, result);
    }

    [Test]
    public async Task When_the_lookup_returns_null_should_report_healthy()
    {
        //Arrange
        var state = ActiveState();
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => null, 3);

        //Act
        HealthCheckResult result = default;
        var exception = await TestExceptionRecorder.CaptureAsync(async () => result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None));

        //Assert
        await Assert.That(exception).IsNull();
        await AssertHealthyWithNoConditions(state, result);
    }

    [Test]
    public async Task When_no_topics_are_tripped_should_report_healthy()
    {
        //Arrange
        var state = ActiveState();
        var breaker = new InMemoryOutboxCircuitBreaker();
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => breaker, 3);

        //Act
        HealthCheckResult result = default;
        var exception = await TestExceptionRecorder.CaptureAsync(async () => result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None));

        //Assert
        await Assert.That(exception).IsNull();
        await AssertHealthyWithNoConditions(state, result);
    }

    private static OutboxSweeperState ActiveState()
    {
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Completed);
        return state;
    }

    private static async Task AssertHealthyWithNoConditions(OutboxSweeperState state, HealthCheckResult result)
    {
        await Assert.That(state.Read().IsActive).IsTrue();
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
        await Assert.That((await Assert.That(result.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEmpty();
    }
}
