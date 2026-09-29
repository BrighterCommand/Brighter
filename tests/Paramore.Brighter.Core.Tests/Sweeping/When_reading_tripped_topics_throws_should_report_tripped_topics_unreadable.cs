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

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class When_reading_tripped_topics_throws_should_report_tripped_topics_unreadable_tests
{
    [Test]
    public async Task When_reading_tripped_topics_throws_should_report_tripped_topics_unreadable()
    {
        //Arrange
        var marker = $"unreadable-topics-{Guid.NewGuid():N}";
        var state = ActiveStateWithThreeFailedSweeps();
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => new ThrowingOutboxCircuitBreaker(marker), 3);

        //Act
        HealthCheckResult result = default;
        var exception = await TestExceptionRecorder.CaptureAsync(async () => result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None));

        //Assert
        await Assert.That(exception).IsNull();
        await AssertUnreadable(state, result);
        await AssertLoggedAtError(marker);
    }

    [Test]
    public async Task When_the_lookup_delegate_throws_should_report_tripped_topics_unreadable()
    {
        //Arrange
        var marker = $"unreadable-lookup-{Guid.NewGuid():N}";
        var state = ActiveStateWithThreeFailedSweeps();
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => throw new InvalidOperationException(marker), 3);

        //Act
        HealthCheckResult result = default;
        var exception = await TestExceptionRecorder.CaptureAsync(async () => result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None));

        //Assert
        await Assert.That(exception).IsNull();
        await AssertUnreadable(state, result);
        await AssertLoggedAtError(marker);
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

    private static async Task AssertUnreadable(OutboxSweeperState state, HealthCheckResult result)
    {
        await Assert.That(state.Read().IsActive).IsTrue();
        await Assert.That(result.Status).IsEqualTo(HealthStatus.Degraded);
        await Assert.That(result.Status).IsNotEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Data[RemedyCheckConvention.RemedyKindKey]).IsEqualTo(RemedyCheckConvention.Name(RemedyKind.Investigate));
        await Assert.That((await Assert.That(result.Data[RemedyCheckConvention.ConditionsKey]).IsTypeOf<string[]>())).IsEqualTo(new[] { "consecutive_failed_sweeps", "tripped_topics_unreadable" });
        var unreadable = ((IDictionary<string, object>)(((IDictionary<string, object>)(await Assert.That(result.Data["tripped_topics_unreadable"]).IsAssignableTo<IDictionary<string, object>>()))));
        await Assert.That(unreadable["exception_type"]).IsEqualTo("System.InvalidOperationException");
        await Assert.That(result.Description).Contains("could not be read");
    }

    private static async Task AssertLoggedAtError(string marker)
    {
        var logged = CapturingLogSink.Instance.Find(typeof(OutboxSweeperInvestigateHealthCheck).FullName!, marker);
        var single = await Assert.That(logged).HasSingleItem();
        await Assert.That(single.Level).IsEqualTo(Serilog.Events.LogEventLevel.Error);
        await Assert.That(single.Exception).IsTypeOf<InvalidOperationException>();
    }
}
