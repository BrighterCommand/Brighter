using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class When_a_sweep_runs_past_the_hang_threshold_should_report_a_hung_sweep_tests
{
    [Test]
    [Arguments(29)]
    [Arguments(30)]
    public async Task When_a_sweep_runs_no_longer_than_the_hang_threshold_should_report_no_sweep_is_hung(int runningSeconds)
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 10 };
        var state = RunningSweep(TimeSpan.FromSeconds(runningSeconds));
        var check = new OutboxSweeperRestartHealthCheck(state, options);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Description).Contains("no outbox sweep is hung");
        await Assert.That(result.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Restart));
        await Assert.That((await Assert.That(result.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEmpty();
    }

    [Test]
    public async Task When_no_sweep_is_in_progress_should_report_no_sweep_is_hung()
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 10 };
        var state = new OutboxSweeperState(new FakeTimeProvider());
        var check = new OutboxSweeperRestartHealthCheck(state, options);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Description).Contains("no outbox sweep is hung");
        await Assert.That(result.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Restart));
        await Assert.That((await Assert.That(result.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEmpty();
    }

    [Test]
    public async Task When_a_sweep_runs_past_the_hang_threshold_should_report_a_hung_sweep()
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 10 };
        var state = RunningSweep(TimeSpan.FromSeconds(31));
        var check = new OutboxSweeperRestartHealthCheck(state, options);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That(result.Description).Contains("31s");
        await Assert.That(result.Description).Contains("30s");
        await Assert.That(result.Description).Contains("10s");
        await Assert.That(result.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Restart));
        var conditions = await Assert.That(result.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>();
        await Assert.That(conditions).IsEqualTo(new[] { "hung_sweep" });
        var hung = ((IDictionary<string, object>)(((IDictionary<string, object>)(await Assert.That(result.Data["hung_sweep"]).IsAssignableTo<IDictionary<string, object>>()))));
        await Assert.That((await Assert.That(hung["running_time_seconds"]).IsTypeOf<double>())).IsEqualTo(31.0);
        await Assert.That((await Assert.That(hung["hang_threshold_seconds"]).IsTypeOf<double>())).IsEqualTo(30.0);
        await Assert.That((await Assert.That(hung["timer_interval_seconds"]).IsTypeOf<double>())).IsEqualTo(10.0);
    }

    [Test]
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
        await Assert.That(healthy.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(healthy.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Restart));
        await Assert.That(degraded.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That(degraded.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Restart));
        var hung = ((IDictionary<string, object>)(((IDictionary<string, object>)(await Assert.That(degraded.Data["hung_sweep"]).IsAssignableTo<IDictionary<string, object>>()))));
        await Assert.That((await Assert.That(hung["timer_interval_seconds"]).IsTypeOf<double>())).IsEqualTo(defaultInterval.TotalSeconds);
        await Assert.That((await Assert.That(hung["hang_threshold_seconds"]).IsTypeOf<double>())).IsEqualTo(threshold.TotalSeconds);
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
