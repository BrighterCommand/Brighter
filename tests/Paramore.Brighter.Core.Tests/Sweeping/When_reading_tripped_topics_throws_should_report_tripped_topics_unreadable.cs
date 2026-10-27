using System;
using System.Collections.Generic;
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

public class When_reading_tripped_topics_throws_should_report_tripped_topics_unreadable_tests
{
    [Fact]
    public async Task When_reading_tripped_topics_throws_should_report_tripped_topics_unreadable()
    {
        //Arrange
        var marker = $"unreadable-topics-{Guid.NewGuid():N}";
        var state = ActiveStateWithThreeFailedSweeps();
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => new ThrowingOutboxCircuitBreaker(marker), 3);

        //Act
        HealthCheckResult result = default;
        var exception = await Record.ExceptionAsync(async () => result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None));

        //Assert
        Assert.Null(exception);
        AssertUnreadable(state, result);
        AssertLoggedAtError(marker);
    }

    [Fact]
    public async Task When_the_lookup_delegate_throws_should_report_tripped_topics_unreadable()
    {
        //Arrange
        var marker = $"unreadable-lookup-{Guid.NewGuid():N}";
        var state = ActiveStateWithThreeFailedSweeps();
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => throw new InvalidOperationException(marker), 3);

        //Act
        HealthCheckResult result = default;
        var exception = await Record.ExceptionAsync(async () => result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None));

        //Assert
        Assert.Null(exception);
        AssertUnreadable(state, result);
        AssertLoggedAtError(marker);
    }

    private static OutboxSweeperState ActiveStateWithThreeFailedSweeps()
    {
        var state = new OutboxSweeperState(new FakeTimeProvider());
        for (var i = 0; i < 3; i++)
        {
            state.SweepStarted();
            state.LockObtained();
            state.SweepEnded(SweepOutcome.Failed);
        }
        return state;
    }

    private static void AssertUnreadable(OutboxSweeperState state, HealthCheckResult result)
    {
        Assert.True(state.Read().IsActive);
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.NotEqual(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), result.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Equal(new[] { "consecutive_failed_sweeps", "tripped_topics_unreadable" }, Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
        var unreadable = Assert.IsAssignableFrom<IDictionary<string, object>>(result.Data["tripped_topics_unreadable"]);
        Assert.Equal("System.InvalidOperationException", unreadable["exception_type"]);
        Assert.Contains("could not be read", result.Description);
    }

    private static void AssertLoggedAtError(string marker)
    {
        var logged = CapturingLogSink.Instance.Find(typeof(OutboxSweeperInvestigateHealthCheck).FullName!, marker);
        var single = Assert.Single(logged);
        Assert.Equal(Serilog.Events.LogEventLevel.Error, single.Level);
        Assert.IsType<InvalidOperationException>(single.Exception);
    }
}
