#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class DistributedTripRacesEndingReadTests
{
    private const int Repetitions = 1000;
    private const int RacingReads = 3;
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_cooldown = TimeSpan.FromSeconds(50);
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = s_cooldown };

    [Fact]
    public async Task When_a_distributed_trip_races_the_ending_read_should_restart_the_cooldown_at_the_trip()
    {
        // Arrange
        var orders = new RoutingKey("orders");

        for (var repetition = 0; repetition < Repetitions; repetition++)
        {
            var clock = new FakeTimeProvider(s_start);
            var breaker = new DistributedOutboxCircuitBreaker(new InMemoryTrippedTopicStore(), s_options, timeProvider: clock);
            await TripAndWriteAsync(breaker, orders);
            clock.Advance(s_cooldown);

            // Act
            var exceptions = await RaceATripAgainstTheEndingReadsAsync(breaker, orders);

            // Assert
            Assert.Empty(exceptions);
            Assert.Contains(orders, breaker.TrippedTopics);

            clock.Advance(s_cooldown - TimeSpan.FromSeconds(1));
            Assert.Contains(orders, breaker.TrippedTopics);
            Assert.Contains(orders, await breaker.ReadTrippedTopicsAsync());

            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.DoesNotContain(orders, await breaker.ReadTrippedTopicsAsync());
        }
    }

    private static async Task TripAndWriteAsync(DistributedOutboxCircuitBreaker breaker, RoutingKey topic)
    {
        var batch = breaker.BeginTripBatch();
        batch.TripTopic(topic);
        await batch.WriteAsync();
    }

    private static async Task<List<Exception>> RaceATripAgainstTheEndingReadsAsync(DistributedOutboxCircuitBreaker breaker, RoutingKey topic)
    {
        var racers = new List<Func<Task>>
        {
            () => TripAndWriteAsync(breaker, topic),
            () => { breaker.TripTopic(new RoutingKey("unrelated")); return Task.CompletedTask; },
            () => { breaker.CoolDown(); return Task.CompletedTask; },
            () => { AssertNoTopicIsListedTwice(breaker); return Task.CompletedTask; }
        };
        racers.AddRange(Enumerable.Repeat<Func<Task>>(async () => await breaker.ReadTrippedTopicsAsync(), RacingReads));

        using var barrier = new Barrier(racers.Count);
        var tasks = racers.Select(racer => Task.Run(async () =>
        {
            barrier.SignalAndWait();
            await racer();
        })).ToArray();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception)
        {
        }

        return tasks.Where(t => t.IsFaulted).Select(t => (Exception)t.Exception!).ToList();
    }

    private static void AssertNoTopicIsListedTwice(DistributedOutboxCircuitBreaker breaker)
    {
        var listed = breaker.TrippedTopics.ToArray();
        if (listed.Length != listed.Distinct().Count())
            throw new InvalidOperationException("A topic was listed twice");
    }
}
