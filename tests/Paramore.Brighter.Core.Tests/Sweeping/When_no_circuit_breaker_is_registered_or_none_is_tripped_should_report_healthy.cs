using System;
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

public class When_no_circuit_breaker_is_registered_or_none_is_tripped_should_report_healthy_tests
{
    [Fact]
    public async Task When_no_lookup_delegate_is_registered_should_report_healthy()
    {
        //Arrange
        var state = ActiveState();
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), null, 3);

        //Act
        HealthCheckResult result = default;
        var exception = await Record.ExceptionAsync(async () => result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None));

        //Assert
        Assert.Null(exception);
        AssertHealthyWithNoConditions(state, result);
    }

    [Fact]
    public async Task When_the_lookup_returns_null_should_report_healthy()
    {
        //Arrange
        var state = ActiveState();
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => null, 3);

        //Act
        HealthCheckResult result = default;
        var exception = await Record.ExceptionAsync(async () => result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None));

        //Assert
        Assert.Null(exception);
        AssertHealthyWithNoConditions(state, result);
    }

    [Fact]
    public async Task When_no_topics_are_tripped_should_report_healthy()
    {
        //Arrange
        var state = ActiveState();
        var breaker = new InMemoryOutboxCircuitBreaker();
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => breaker, 3);

        //Act
        HealthCheckResult result = default;
        var exception = await Record.ExceptionAsync(async () => result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None));

        //Assert
        Assert.Null(exception);
        AssertHealthyWithNoConditions(state, result);
    }

    private static OutboxSweeperState ActiveState()
    {
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Completed);
        return state;
    }

    private static void AssertHealthyWithNoConditions(OutboxSweeperState state, HealthCheckResult result)
    {
        Assert.True(state.Read().IsActive);
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), result.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Empty(Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
    }
}
