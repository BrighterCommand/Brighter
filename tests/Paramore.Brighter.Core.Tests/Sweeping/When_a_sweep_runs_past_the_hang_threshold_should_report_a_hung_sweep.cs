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

public class When_a_sweep_runs_past_the_hang_threshold_should_report_a_hung_sweep_tests
{
    [Theory]
    [InlineData(29)]
    [InlineData(30)]
    public async Task When_a_sweep_runs_no_longer_than_the_hang_threshold_should_report_no_sweep_is_hung(int runningSeconds)
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 10 };
        var state = RunningSweep(TimeSpan.FromSeconds(runningSeconds));
        var check = new OutboxSweeperRestartHealthCheck(state, options);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("no outbox sweep is hung", result.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), result.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Empty(Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
    }

    [Fact]
    public async Task When_no_sweep_is_in_progress_should_report_no_sweep_is_hung()
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 10 };
        var state = new OutboxSweeperState(new FakeTimeProvider());
        var check = new OutboxSweeperRestartHealthCheck(state, options);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("no outbox sweep is hung", result.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), result.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Empty(Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
    }

    [Fact]
    public async Task When_a_sweep_runs_past_the_hang_threshold_should_report_a_hung_sweep()
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 10 };
        var state = RunningSweep(TimeSpan.FromSeconds(31));
        var check = new OutboxSweeperRestartHealthCheck(state, options);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("31s", result.Description);
        Assert.Contains("30s", result.Description);
        Assert.Contains("10s", result.Description);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), result.Data[RemedyCheckConvention.RemedyKindKey]);
        var conditions = Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]);
        Assert.Equal(new[] { "hung_sweep" }, conditions);
        var hung = Assert.IsAssignableFrom<IDictionary<string, object>>(result.Data["hung_sweep"]);
        Assert.Equal(31.0, Assert.IsType<double>(hung["running_time_seconds"]));
        Assert.Equal(30.0, Assert.IsType<double>(hung["hang_threshold_seconds"]));
        Assert.Equal(10.0, Assert.IsType<double>(hung["timer_interval_seconds"]));
    }

    [Fact]
    public async Task When_options_are_null_should_use_the_default_timer_interval_for_the_threshold()
    {
        //Arrange
        var defaultInterval = TimeSpan.FromSeconds(new TimedOutboxSweeperOptions().TimerInterval);
        var threshold = defaultInterval * 3;
        var atThreshold = new OutboxSweeperRestartHealthCheck(RunningSweep(threshold), null);
        var pastThreshold = new OutboxSweeperRestartHealthCheck(RunningSweep(threshold + TimeSpan.FromSeconds(1)), null);

        //Act
        var healthy = await atThreshold.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        var degraded = await pastThreshold.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.Equal(HealthStatus.Healthy, healthy.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), healthy.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Equal(HealthStatus.Degraded, degraded.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), degraded.Data[RemedyCheckConvention.RemedyKindKey]);
        var hung = Assert.IsAssignableFrom<IDictionary<string, object>>(degraded.Data["hung_sweep"]);
        Assert.Equal(defaultInterval.TotalSeconds, Assert.IsType<double>(hung["timer_interval_seconds"]));
        Assert.Equal(threshold.TotalSeconds, Assert.IsType<double>(hung["hang_threshold_seconds"]));
    }

    private static OutboxSweeperState RunningSweep(TimeSpan runningTime)
    {
        var timeProvider = new FakeTimeProvider();
        var state = new OutboxSweeperState(timeProvider);
        state.SweepStarted();
        timeProvider.Advance(runningTime);
        return state;
    }
}
