using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class When_the_sweeper_is_not_active_should_not_report_tripped_topics_tests
{
    [Fact]
    public async Task When_the_sweeper_has_never_ended_a_sweep_should_report_healthy()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());

        //Act
        var result = await CheckWithOrdersTripped(state);

        //Assert
        AssertHealthyAndInactive(state, result);
    }

    [Fact]
    public async Task When_the_last_sweep_found_the_lock_unavailable_should_report_healthy()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.SweepEnded(SweepOutcome.LockUnavailable);

        //Act
        var result = await CheckWithOrdersTripped(state);

        //Assert
        AssertHealthyAndInactive(state, result);
    }

    [Fact]
    public async Task When_a_sweep_is_acquiring_the_lock_after_a_completed_sweep_should_report_healthy()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Completed);
        state.SweepStarted();

        //Act
        var result = await CheckWithOrdersTripped(state);

        //Assert
        AssertHealthyAndInactive(state, result);
    }

    [Fact]
    public async Task When_the_last_sweep_failed_without_the_lock_should_report_healthy()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.SweepEnded(SweepOutcome.Failed);

        //Act
        var result = await CheckWithOrdersTripped(state);

        //Assert
        AssertHealthyAndInactive(state, result);
    }

    [Fact]
    public async Task When_the_sweeper_is_stopped_should_report_healthy()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Completed);
        state.Stopped();

        //Act
        var result = await CheckWithOrdersTripped(state);

        //Assert
        AssertHealthyAndInactive(state, result);
    }

    [Fact]
    public async Task When_the_sweeper_is_not_active_should_never_read_the_circuit_breaker()
    {
        //Arrange
        var marker = $"inactive-breaker-{Guid.NewGuid():N}";
        var state = new OutboxSweeperState(new FakeTimeProvider());
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => new ThrowingOutboxCircuitBreaker(marker), 3);

        //Act
        HealthCheckResult result = default;
        var exception = await Record.ExceptionAsync(async () => result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None));

        //Assert
        Assert.Null(exception);
        AssertHealthyAndInactive(state, result);
        Assert.Empty(CapturingLogSink.Instance.Find(typeof(OutboxSweeperInvestigateHealthCheck).FullName!, marker));
        Assert.Empty(CapturingLogSink.Instance.Find(typeof(SweeperMeter).FullName!, marker));
    }

    private static async Task<HealthCheckResult> CheckWithOrdersTripped(OutboxSweeperState state)
    {
        var breaker = new InMemoryOutboxCircuitBreaker();
        breaker.TripTopic(new RoutingKey("orders"));
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => breaker, 3);
        return await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
    }

    private static void AssertHealthyAndInactive(OutboxSweeperState state, HealthCheckResult result)
    {
        Assert.False(state.Read().IsActive);
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.NotEqual(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), result.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Empty(Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
    }
}
