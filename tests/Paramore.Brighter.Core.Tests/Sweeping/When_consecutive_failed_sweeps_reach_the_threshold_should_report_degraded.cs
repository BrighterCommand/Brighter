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

public class When_consecutive_failed_sweeps_reach_the_threshold_should_report_degraded_tests
{
    [Test]
    public async Task When_fewer_sweeps_have_failed_than_the_threshold_should_report_healthy()
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 10 };
        var check = new OutboxSweeperInvestigateHealthCheck(FailedSweeps(2), options, null, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Description).Contains("condition");
        await Assert.That(result.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
        await Assert.That((await Assert.That(result.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEmpty();
    }

    [Test]
    [Arguments(3)]
    [Arguments(4)]
    public async Task When_the_failed_sweeps_reach_the_threshold_should_report_degraded(int failures)
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 10 };
        var check = new OutboxSweeperInvestigateHealthCheck(FailedSweeps(failures), options, null, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That(result.Description).Contains(failures.ToString());
        await Assert.That(result.Description).Contains("3");
        await Assert.That(result.Description).Contains("10s");
        await Assert.That(result.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
        await Assert.That((await Assert.That(result.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEqualTo(new[] { "consecutive_failed_sweeps" });
        var failed = ((IDictionary<string, object>)(((IDictionary<string, object>)(await Assert.That(result.Data["consecutive_failed_sweeps"]).IsAssignableTo<IDictionary<string, object>>()))));
        await Assert.That((await Assert.That(failed["count"]).IsTypeOf<int>())).IsEqualTo(failures);
        await Assert.That((await Assert.That(failed["threshold"]).IsTypeOf<int>())).IsEqualTo(3);
        await Assert.That((await Assert.That(failed["timer_interval_seconds"]).IsTypeOf<double>())).IsEqualTo(10.0);
    }

    [Test]
    public async Task When_options_are_null_should_use_the_default_timer_interval()
    {
        //Arrange
        var check = new OutboxSweeperInvestigateHealthCheck(FailedSweeps(3), null, null, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That(result.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
        var failed = ((IDictionary<string, object>)(((IDictionary<string, object>)(await Assert.That(result.Data["consecutive_failed_sweeps"]).IsAssignableTo<IDictionary<string, object>>()))));
        await Assert.That((await Assert.That(failed["timer_interval_seconds"]).IsTypeOf<double>())).IsEqualTo((double)new TimedOutboxSweeperOptions().TimerInterval);
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
