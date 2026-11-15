using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class When_the_failure_threshold_is_one_or_not_positive_should_degrade_or_reject_tests
{
    [Test]
    public async Task When_the_failure_threshold_is_one_should_degrade_after_one_failed_sweep()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.SweepEnded(SweepOutcome.Failed);
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions { TimerInterval = 10 }, null, 1);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That(result.Status).IsNotEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
        await Assert.That((await Assert.That(result.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEqualTo(new[] { "consecutive_failed_sweeps" });
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task When_the_failure_threshold_is_not_positive_should_throw(int failureThreshold)
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 10 };

        //Act
        var exception = TestExceptionRecorder.Capture(() => new OutboxSweeperInvestigateHealthCheck(null, options, null, failureThreshold));

        //Assert
        var argumentException = await Assert.That(exception).IsTypeOf<ArgumentOutOfRangeException>();
        await Assert.That(argumentException.ParamName).IsEqualTo("failureThreshold");
    }
}
