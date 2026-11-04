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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperRefreshOnLockUnavailableTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = TimeSpan.FromSeconds(50), ClockSkewLeeway = TimeSpan.Zero };

    [Fact]
    public async Task When_every_sweep_is_lock_unavailable_should_still_refresh_the_shared_breakers_view()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var store = new InMemoryTrippedTopicStore();
        var instanceA = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock);
        var instanceB = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock);
        var orders = new RoutingKey("orders");
        instanceA.TripTopic(orders);
        var written = SpinWait.SpinUntil(() => store.ReadAsync(clock.GetUtcNow(), s_options.CooldownDuration).GetAwaiter().GetResult().Live.Count > 0, s_timeout);
        using var outbox = new SweepableOutbox(instanceB);
        using var measurements = new RecordedSweeperMeasurements();
        var sweeper = new TimedOutboxSweeper(
            outbox.ScopeFactory,
            new HeldDistributedLock(),
            new TimedOutboxSweeperOptions { TimerInterval = 5, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: clock,
            meter: new SweeperMeter(measurements.MeterFactory),
            circuitBreaker: instanceB);

        // Act
        await sweeper.StartAsync(CancellationToken.None);
        var swept = measurements.WaitFor(m => m.Of(RecordedSweeperMeasurements.Sweeps).Count == 1, s_timeout);
        var listed = SpinWait.SpinUntil(() => instanceB.TrippedTopics.Contains(orders), s_timeout);
        await sweeper.StopAsync(CancellationToken.None);
        sweeper.Dispose();

        // Assert
        Assert.True(written, "Instance A did not share its trip");
        Assert.True(swept, "The sweeper recorded no sweep");
        Assert.Equal("lock_unavailable", measurements.Of(RecordedSweeperMeasurements.Sweeps).Single().Outcome);
        Assert.True(listed, "Instance B did not list the topic instance A tripped");
    }
}
