#region License

/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

#nullable enable

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

public class When_reading_tripped_topics_throws_on_a_shared_breaker_in_fallback_should_report_unreadable_and_store_unavailable_tests
{
    [Fact]
    public async Task When_reading_tripped_topics_throws_on_a_shared_breaker_in_fallback_should_report_unreadable_and_store_unavailable()
    {
        //Arrange
        var state = FailedSweepsState();
        var breaker = new ThrowingSharedOutboxCircuitBreaker(new TrippedTopicStoreStatus(IsUsingFallback: true, LastFailure: "timeout"));
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => breaker, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(new[] { "consecutive_failed_sweeps", "tripped_topics_unreadable", "tripped_topics_store_unavailable" }, Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
        var unavailable = Assert.IsAssignableFrom<IDictionary<string, object>>(result.Data["tripped_topics_store_unavailable"]);
        Assert.Equal("timeout", unavailable["exception_type"]);
    }

    [Fact]
    public async Task When_reading_tripped_topics_throws_on_a_shared_breaker_not_in_fallback_should_report_unreadable_only()
    {
        //Arrange
        var state = FailedSweepsState();
        var breaker = new ThrowingSharedOutboxCircuitBreaker(new TrippedTopicStoreStatus(IsUsingFallback: false, LastFailure: null));
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => breaker, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(new[] { "consecutive_failed_sweeps", "tripped_topics_unreadable" }, Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
    }

    private static OutboxSweeperState FailedSweepsState()
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
}
