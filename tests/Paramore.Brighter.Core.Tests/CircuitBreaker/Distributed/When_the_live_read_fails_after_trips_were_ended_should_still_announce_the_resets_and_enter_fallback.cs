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
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class DistributedBreakerLiveReadFailureTests
{
    private static readonly TimeSpan s_cooldown = TimeSpan.FromSeconds(50);
    private readonly RoutingKey _orders = new("orders");

    private static int Announced(string operation) => TestCorrelator.GetLogEventsFromCurrentContext()
        .Count(e => e.MessageTemplate.Text.StartsWith($"Circuit breaker {operation} for topic"));

    [Fact]
    public async Task When_the_live_read_fails_after_trips_were_ended_should_still_announce_the_resets_and_enter_fallback()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            var clock = new FakeTimeProvider();
            var options = new OutboxCircuitBreakerOptions { CooldownDuration = s_cooldown, ClockSkewLeeway = TimeSpan.Zero };
            var breaker = new DistributedOutboxCircuitBreaker(
                new LiveReadFailingTrippedTopicStore(new InMemoryTrippedTopicStore()), options, timeProvider: clock);
            var batch = breaker.BeginTripBatch();
            batch.TripTopic(_orders);
            await batch.WriteAsync();
            clock.Advance(s_cooldown);

            // Act
            var topics = await breaker.ReadTrippedTopicsAsync();

            // Assert
            Assert.Equal(1, Announced("reset"));
            Assert.Empty(topics);
            Assert.True(breaker.StoreStatus.IsUsingFallback);
            Assert.Equal("InvalidOperationException", breaker.StoreStatus.LastFailure);
        }
    }
}
