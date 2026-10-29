using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class When_the_hang_threshold_is_overridden_should_judge_a_hung_sweep_against_it_tests
{
    [Fact]
    public async Task When_the_hang_threshold_is_overridden_should_not_report_a_sweep_within_it_as_hung()
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 10 };
        var state = RunningSweep(TimeSpan.FromSeconds(90));
        var check = new OutboxSweeperRestartHealthCheck(state, options, TimeSpan.FromMinutes(2));

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), result.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Contains("120s", result.Description);
    }

    [Fact]
    public async Task When_the_hang_threshold_is_overridden_should_report_a_sweep_past_it_as_hung()
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 10 };
        var state = RunningSweep(TimeSpan.FromSeconds(121));
        var check = new OutboxSweeperRestartHealthCheck(state, options, TimeSpan.FromMinutes(2));

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), result.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Contains("121s", result.Description);
        Assert.Contains("120s", result.Description);
        Assert.Contains("10s", result.Description);
    }

    [Fact]
    public async Task When_a_sweep_runs_exactly_the_default_threshold_should_report_it_as_three_seconds()
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 1 };
        var state = RunningSweep(TimeSpan.FromSeconds(3));
        var check = new OutboxSweeperRestartHealthCheck(state, options);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), result.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Contains("3s", result.Description);
    }

    [Fact]
    public async Task When_a_sweep_runs_a_millisecond_past_the_default_threshold_should_report_three_decimals()
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 1 };
        var state = RunningSweep(TimeSpan.FromMilliseconds(3001));
        var check = new OutboxSweeperRestartHealthCheck(state, options);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Restart), result.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Contains("3.001s", result.Description);
        Assert.Contains("3s", result.Description);
        Assert.Contains("1s", result.Description);
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
