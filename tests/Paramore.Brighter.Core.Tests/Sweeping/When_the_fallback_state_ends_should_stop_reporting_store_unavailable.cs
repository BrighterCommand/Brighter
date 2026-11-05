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

public class When_the_fallback_state_ends_should_stop_reporting_store_unavailable_tests
{
    [Fact]
    public async Task When_the_fallback_state_ends_should_stop_reporting_store_unavailable()
    {
        //Arrange
        var state = RunningState();
        var store = new ReadsFailingOnDemandTrippedTopicStore(new InMemoryTrippedTopicStore()) { FailReads = true };
        var breaker = new DistributedOutboxCircuitBreaker(store, new OutboxCircuitBreakerOptions(), timeProvider: new FakeTimeProvider());
        await breaker.ReadTrippedTopicsAsync();
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => breaker, 3);
        var whileFailing = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        store.FailReads = false;
        await breaker.ReadTrippedTopicsAsync();

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.Contains("tripped_topics_store_unavailable", Assert.IsType<string[]>(whileFailing.Data[RemedyCheckConvention.ConditionsKey]));
        Assert.False(result.Data.ContainsKey("tripped_topics_store_unavailable"));
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task When_a_shared_breaker_has_not_yet_read_should_not_report_store_unavailable()
    {
        //Arrange
        var state = RunningState();
        var breaker = new DistributedOutboxCircuitBreaker(new InMemoryTrippedTopicStore(), new OutboxCircuitBreakerOptions(), timeProvider: new FakeTimeProvider());
        var check = new OutboxSweeperInvestigateHealthCheck(state, new TimedOutboxSweeperOptions(), () => breaker, 3);

        //Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        //Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.False(result.Data.ContainsKey("tripped_topics_store_unavailable"));
    }

    private static OutboxSweeperState RunningState()
    {
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.SweepEnded(SweepOutcome.LockUnavailable);
        return state;
    }
}
