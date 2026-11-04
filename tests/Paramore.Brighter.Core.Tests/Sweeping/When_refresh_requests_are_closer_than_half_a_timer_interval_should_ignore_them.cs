#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperRefreshRequestSpacingTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_refresh_requests_are_closer_than_half_a_timer_interval_should_ignore_them()
    {
        // Arrange
        var fake = new FakeTimeProvider();
        var clock = new TimerCountingTimeProvider(fake);
        var store = new CountingTrippedTopicStore();
        var breaker = new DistributedOutboxCircuitBreaker(store, new OutboxCircuitBreakerOptions(), timeProvider: clock);
        using var outbox = new SweepableOutbox(breaker);
        using var measurements = new RecordedSweeperMeasurements();
        var sweeper = new TimedOutboxSweeper(
            outbox.ScopeFactory,
            new HeldDistributedLock(),
            new TimedOutboxSweeperOptions { TimerInterval = 10, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: clock,
            meter: new SweeperMeter(measurements.MeterFactory),
            circuitBreaker: breaker);

        // Act
        await sweeper.StartAsync(CancellationToken.None);
        var firstSwept = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Count == 1, s_timeout);
        var firstRead = SpinWait.SpinUntil(() => store.Reads == 1, s_timeout);
        var firstWaiting = clock.WaitForTimers(1, s_timeout);
        fake.Advance(TimeSpan.FromSeconds(19));
        var lateSwept = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Count == 2, s_timeout);
        var lateRead = SpinWait.SpinUntil(() => store.Reads == 2, s_timeout);
        var secondWaiting = clock.WaitForTimers(2, s_timeout);
        fake.Advance(TimeSpan.FromSeconds(1));
        var closeSwept = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Count == 3, s_timeout);
        await Task.Delay(300);
        await sweeper.StopAsync(CancellationToken.None);
        sweeper.Dispose();

        // Assert
        Assert.True(firstSwept && firstRead && firstWaiting, "The first sweep did not request a refresh");
        Assert.True(lateSwept && lateRead && secondWaiting, "The late sweep did not request a refresh");
        Assert.True(closeSwept, "The sweep one second later did not complete");
        Assert.Equal(2, store.Reads);
    }
}
