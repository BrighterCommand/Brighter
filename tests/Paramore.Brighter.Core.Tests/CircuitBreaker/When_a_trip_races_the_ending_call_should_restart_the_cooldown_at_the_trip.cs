using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker;

public class InMemoryTripRacesEndingCallTests
{
    private const int REPETITIONS = 1000;
    private const int COOLDOWN_COUNT = 2;
    private const int RACING_COOLDOWNS = 2;
    private static readonly TimeSpan s_maximumTripDuration = TimeSpan.FromMinutes(5);

    [Fact]
    public void When_a_trip_races_the_ending_call_should_restart_the_cooldown_at_the_trip()
    {
        // Arrange
        var orders = new RoutingKey("orders");

        for (var repetition = 0; repetition < REPETITIONS; repetition++)
        {
            var (breaker, _) = BuildBreakerWithTopicAboutToEnd(orders);

            // Act
            var exceptions = RaceATripAgainstTheEndingCalls(breaker, orders);

            // Assert
            Assert.Empty(exceptions);
            Assert.Contains(orders, breaker.TrippedTopics);

            var callsThatLeaveItListed = CountCoolDownsThatLeaveTheTopicListed(breaker, orders);
            Assert.InRange(callsThatLeaveItListed, COOLDOWN_COUNT - RACING_COOLDOWNS, COOLDOWN_COUNT);
            Assert.DoesNotContain(orders, breaker.TrippedTopics);
        }
    }

    [Fact]
    public void When_a_trip_has_raced_the_ending_call_should_end_at_the_maximum_trip_duration()
    {
        // Arrange
        var orders = new RoutingKey("orders");
        var (breaker, clock) = BuildBreakerWithTopicAboutToEnd(orders);
        RaceATripAgainstTheEndingCalls(breaker, orders);

        // Act
        clock.Advance(s_maximumTripDuration);

        // Assert
        Assert.DoesNotContain(orders, breaker.TrippedTopics);
    }

    private static (InMemoryOutboxCircuitBreaker Breaker, FakeTimeProvider Clock) BuildBreakerWithTopicAboutToEnd(RoutingKey topic)
    {
        var clock = new FakeTimeProvider();
        var breaker = new InMemoryOutboxCircuitBreaker(
            new OutboxCircuitBreakerOptions { CooldownCount = COOLDOWN_COUNT, MaximumTripDuration = s_maximumTripDuration },
            timeProvider: clock);
        breaker.TripTopic(topic);
        for (var cycle = 0; cycle < COOLDOWN_COUNT; cycle++)
            breaker.CoolDown();
        return (breaker, clock);
    }

    private static List<Exception> RaceATripAgainstTheEndingCalls(InMemoryOutboxCircuitBreaker breaker, RoutingKey topic)
    {
        var racers = new List<Action> { () => breaker.TripTopic(topic) };
        racers.AddRange(Enumerable.Repeat<Action>(breaker.CoolDown, RACING_COOLDOWNS));
        racers.AddRange(Enumerable.Repeat<Action>(() => AssertNoTopicIsListedTwice(breaker), 2));

        using var barrier = new Barrier(racers.Count);
        var tasks = racers.Select(racer => Task.Run(() =>
        {
            barrier.SignalAndWait();
            racer();
        })).ToArray();

        try
        {
            Task.WaitAll(tasks);
        }
        catch (AggregateException)
        {
        }

        return tasks.Where(t => t.IsFaulted).Select(t => (Exception)t.Exception!).ToList();
    }

    private static void AssertNoTopicIsListedTwice(InMemoryOutboxCircuitBreaker breaker)
    {
        var listed = breaker.TrippedTopics.ToArray();
        if (listed.Length != listed.Distinct().Count())
            throw new InvalidOperationException("A topic was listed twice");
    }

    private static int CountCoolDownsThatLeaveTheTopicListed(InMemoryOutboxCircuitBreaker breaker, RoutingKey topic)
    {
        var calls = 0;
        while (true)
        {
            breaker.CoolDown();
            if (!breaker.TrippedTopics.Contains(topic))
                return calls;
            calls++;
        }
    }
}
