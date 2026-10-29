using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class When_consecutive_failed_sweeps_reach_the_threshold_should_report_degraded_tests
{
    [Fact]
    public async Task When_fewer_sweeps_have_failed_than_the_threshold_should_report_healthy()
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 10 };
        var check = new OutboxSweeperInvestigateHealthCheck(FailedSweeps(2), options, null, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("condition", result.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), result.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Empty(Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public async Task When_the_failed_sweeps_reach_the_threshold_should_report_degraded(int failures)
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 10 };
        var check = new OutboxSweeperInvestigateHealthCheck(FailedSweeps(failures), options, null, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains(failures.ToString(), result.Description);
        Assert.Contains("3", result.Description);
        Assert.Contains("10s", result.Description);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), result.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Equal(new[] { "consecutive_failed_sweeps" }, Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
        var failed = Assert.IsAssignableFrom<IDictionary<string, object>>(result.Data["consecutive_failed_sweeps"]);
        Assert.Equal(failures, Assert.IsType<int>(failed["count"]));
        Assert.Equal(3, Assert.IsType<int>(failed["threshold"]));
        Assert.Equal(10.0, Assert.IsType<double>(failed["timer_interval_seconds"]));
    }

    [Fact]
    public async Task When_options_are_null_should_use_the_default_timer_interval()
    {
        //Arrange
        var check = new OutboxSweeperInvestigateHealthCheck(FailedSweeps(3), null, null, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), result.Data[RemedyCheckConvention.RemedyKindKey]);
        var failed = Assert.IsAssignableFrom<IDictionary<string, object>>(result.Data["consecutive_failed_sweeps"]);
        Assert.Equal((double)new TimedOutboxSweeperOptions().TimerInterval, Assert.IsType<double>(failed["timer_interval_seconds"]));
    }

    private static OutboxSweeperState FailedSweeps(int count)
    {
        var state = new OutboxSweeperState(new FakeTimeProvider());
        for (var i = 0; i < count; i++)
        {
            state.SweepStarted();
            state.SweepEnded(SweepOutcome.Failed);
        }
        return state;
    }
}
