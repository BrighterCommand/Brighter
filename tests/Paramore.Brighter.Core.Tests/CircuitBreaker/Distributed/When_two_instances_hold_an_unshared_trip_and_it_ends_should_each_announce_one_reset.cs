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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class When_two_instances_hold_an_unshared_trip_and_it_ends_should_each_announce_one_reset
{
    private static readonly TimeSpan s_cooldown = TimeSpan.FromSeconds(50);
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = s_cooldown, ClockSkewLeeway = TimeSpan.Zero };
    private readonly RoutingKey _orders = new("orders");
    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryTrippedTopicStore _shared = new();
    private readonly WritesFailingOnDemandTrippedTopicStore _storeA;
    private readonly WritesFailingOnDemandTrippedTopicStore _storeB;
    private readonly DistributedOutboxCircuitBreaker _a;
    private readonly DistributedOutboxCircuitBreaker _b;

    public When_two_instances_hold_an_unshared_trip_and_it_ends_should_each_announce_one_reset()
    {
        _storeA = new WritesFailingOnDemandTrippedTopicStore(_shared) { FailWrites = true };
        _storeB = new WritesFailingOnDemandTrippedTopicStore(_shared) { FailWrites = true };
        _a = new DistributedOutboxCircuitBreaker(_storeA, s_options, timeProvider: _clock);
        _b = new DistributedOutboxCircuitBreaker(_storeB, s_options, timeProvider: _clock);
    }

    private static async Task TripUnshared(DistributedOutboxCircuitBreaker breaker, RoutingKey topic)
    {
        var batch = breaker.BeginTripBatch();
        batch.TripTopic(topic);
        await batch.WriteAsync();
    }

    private static int Announced(string operation) => TestCorrelator.GetLogEventsFromCurrentContext()
        .Count(e => e.MessageTemplate.Text.StartsWith($"Circuit breaker {operation} for topic"));

    [Fact]
    public async Task When_the_unshared_trips_end_should_announce_one_reset_per_instance_from_the_first_announcing_call()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            await TripUnshared(_a, _orders);
            await TripUnshared(_b, _orders);
            _clock.Advance(s_cooldown);

            // Act
            _a.CoolDown();
            _a.CoolDown();
            await _b.ReadTrippedTopicsAsync();
            await _b.ReadTrippedTopicsAsync();

            // Assert
            Assert.Equal(2, Announced("tripped"));
            Assert.Equal(2, Announced("reset"));
        }
    }

    [Fact]
    public async Task When_gauge_check_and_trip_reads_run_after_the_end_should_announce_nothing()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            await TripUnshared(_a, _orders);
            _clock.Advance(s_cooldown);

            // Act
            _ = _a.StoreStatus;
            _ = _a.TrippedTopics.ToList();

            // Assert
            Assert.Equal(0, Announced("reset"));
        }
    }

    [Fact]
    public async Task When_the_fallback_has_already_ended_should_still_announce_one_reset_from_the_first_cooldown()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            await TripUnshared(_a, _orders);
            _clock.Advance(s_cooldown);
            var isUsingFallback = _a.StoreStatus.IsUsingFallback;

            // Act
            _a.CoolDown();
            _a.CoolDown();

            // Assert
            Assert.False(isUsingFallback);
            Assert.Equal(1, Announced("reset"));
        }
    }

    [Fact]
    public async Task When_a_later_write_shares_a_trip_announced_from_the_view_should_not_announce_it_again_but_still_announce_the_reset()
    {
        using (TestCorrelator.CreateContext())
        {
            // Arrange
            await _shared.WriteAsync([new TopicTrip(_orders, _clock.GetUtcNow())]);
            _clock.Advance(TimeSpan.FromSeconds(25));
            await TripUnshared(_a, _orders);
            _clock.Advance(TimeSpan.FromSeconds(30));
            _storeA.FailWrites = false;

            // Act
            await TripUnshared(_a, _orders);

            // Assert
            Assert.Equal(1, Announced("tripped"));
            Assert.Equal(1, Announced("reset"));
            Assert.False(_a.StoreStatus.IsUsingFallback);
        }
    }
}
