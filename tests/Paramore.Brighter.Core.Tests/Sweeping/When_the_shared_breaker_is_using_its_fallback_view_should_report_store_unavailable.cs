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


using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class When_the_shared_breaker_is_using_its_fallback_view_should_report_store_unavailable_tests
{
    private static readonly TimeSpan s_storeTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task When_the_shared_breaker_is_using_its_fallback_view_should_report_store_unavailable()
    {
        //Arrange
        var state = RunningState();
        var breaker = new DistributedOutboxCircuitBreaker(new ThrowingOnReadTrippedTopicStore(), new OutboxCircuitBreakerOptions(), timeProvider: new FakeTimeProvider());
        await breaker.ReadTrippedTopicsAsync();
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => breaker, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(RemedyCheckConvention.Name(RemedyKind.Investigate), result.Data[RemedyCheckConvention.RemedyKindKey]);
        Assert.Equal(new[] { "tripped_topics_store_unavailable" }, Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
        var unavailable = Assert.IsAssignableFrom<IDictionary<string, object>>(result.Data["tripped_topics_store_unavailable"]);
        Assert.Equal("InvalidOperationException", unavailable["exception_type"]);
        Assert.Equal("The tripped topics could not be read from or written to the shared store (InvalidOperationException); this instance is using its own view.", result.Description);
    }

    [Fact]
    public async Task When_the_shared_store_times_out_should_report_store_unavailable_with_timeout()
    {
        //Arrange
        var state = RunningState();
        var clock = new FakeTimeProvider();
        var breaker = new DistributedOutboxCircuitBreaker(new NeverCompletingTrippedTopicStore(), new OutboxCircuitBreakerOptions { StoreTimeout = s_storeTimeout }, timeProvider: clock);
        var read = breaker.ReadTrippedTopicsAsync();
        clock.Advance(s_storeTimeout);
        await read;
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => breaker, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        var unavailable = Assert.IsAssignableFrom<IDictionary<string, object>>(result.Data["tripped_topics_store_unavailable"]);
        Assert.Equal("timeout", unavailable["exception_type"]);
    }

    [Fact]
    public async Task When_sweeps_have_failed_and_the_store_is_unavailable_should_report_failures_then_trips_then_store_unavailable()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        for (var i = 0; i < 3; i++)
        {
            state.SweepStarted();
            state.LockObtained();
            state.SweepEnded(SweepOutcome.Failed);
        }
        var breaker = new DistributedOutboxCircuitBreaker(new ThrowingOnReadTrippedTopicStore(), new OutboxCircuitBreakerOptions(), timeProvider: new FakeTimeProvider());
        breaker.TripTopic(new RoutingKey("orders"));
        await breaker.ReadTrippedTopicsAsync();
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => breaker, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(new[] { "consecutive_failed_sweeps", "tripped_topics", "tripped_topics_store_unavailable" }, Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
        var unavailable = Assert.IsAssignableFrom<IDictionary<string, object>>(result.Data["tripped_topics_store_unavailable"]);
        Assert.Equal("InvalidOperationException", unavailable["exception_type"]);
    }

    [Fact]
    public async Task When_an_instance_started_during_an_outage_should_report_store_unavailable()
    {
        //Arrange
        var state = RunningState();
        var clock = new FakeTimeProvider();
        var store = new ReadsFailingOnDemandTrippedTopicStore(new InMemoryTrippedTopicStore()) { FailReads = true };
        var breaker = new DistributedOutboxCircuitBreaker(store, new OutboxCircuitBreakerOptions(), timeProvider: clock);
        await breaker.ReadTrippedTopicsAsync();
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => breaker, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.False(state.Read().IsActive);
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(new[] { "tripped_topics_store_unavailable" }, Assert.IsType<string[]>(result.Data[RemedyCheckConvention.ConditionsKey]));
    }

    private static OutboxSweeperState RunningState()
    {
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.SweepEnded(SweepOutcome.LockUnavailable);
        return state;
    }
}
