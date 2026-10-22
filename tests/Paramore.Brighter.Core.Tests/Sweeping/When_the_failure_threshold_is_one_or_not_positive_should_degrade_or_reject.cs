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

public class When_the_failure_threshold_is_one_or_not_positive_should_degrade_or_reject_tests
{
    [Fact]
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
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.NotEqual(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), result.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Equal(new[] { "consecutive_failed_sweeps" }, Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void When_the_failure_threshold_is_not_positive_should_throw(int failureThreshold)
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 10 };

        //Act
        var exception = Record.Exception(() => new OutboxSweeperInvestigateHealthCheck(null, options, null, failureThreshold));

        //Assert
        var argumentException = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal("failureThreshold", argumentException.ParamName);
    }
}
