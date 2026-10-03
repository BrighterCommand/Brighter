#region License

/* The MIT License (MIT)
Copyright © 2025 Michael Freeman <mike.c.freeman@gmail.com>

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

# endregion


using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.CircuitBreaker;

/// <summary>
/// In-memory implementation of IAmAnOutboxCircuitBreaker; Tracks dispatch failures per topic,
/// if a topic is marked as tripped, record that topic as a failure and cooldown by decrementing
/// cool down count.
/// </summary>
/// <param name="options"></param>
/// <param name="tracer">Optional tracer used to export a span for each trip/re-trip/reset transition</param>
/// <param name="instrumentationOptions">How verbose should the exported spans be</param>
public partial class InMemoryOutboxCircuitBreaker(
    OutboxCircuitBreakerOptions? options = null,
    IAmABrighterTracer? tracer = null,
    InstrumentationOptions instrumentationOptions = InstrumentationOptions.All) : IAmAnOutboxCircuitBreaker
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<InMemoryOutboxCircuitBreaker>();

    private readonly OutboxCircuitBreakerOptions _outboxCircuitBreakerOptions = options ?? new OutboxCircuitBreakerOptions();

    private readonly ConcurrentDictionary<RoutingKey, int> _trippedTopics = new();

    /// <summary>
    /// A collection of tripped topics.
    /// </summary>
    public IEnumerable<RoutingKey> TrippedTopics => _trippedTopics.Keys.ToArray();


    /// <summary>
    /// Each time an attempt to ClearOutstandingFromOutbox the IAmAnOutboxCircuitBreaker is cooled down,
    /// this decrements the tripped topics, making them available for publication once the Cooldown
    /// period is set to zero.
    /// </summary>
    public void CoolDown()
    {
        foreach (var trippedTopicsKey in _trippedTopics.Keys)
        {
            // Atomic decrement: AddOrUpdate's compare-and-swap retries against any concurrent TripTopic
            // reset, so a freshly tripped topic is never clobbered by a stale read-modify-write. The
            // returned post-decrement value drives the eviction decision — re-reading the indexer here
            // (as the previous code did) would throw KeyNotFoundException if another CoolDown had already
            // removed the key.
            var cooled = _trippedTopics.AddOrUpdate(trippedTopicsKey, -1, (_, count) => count - 1);

            TryEvictAndAnnounceReset(trippedTopicsKey, cooled);
        }
    }

    private void TryEvictAndAnnounceReset(RoutingKey topic, int cooledValue)
    {
        if (cooledValue >= 0)
            return;

        // Conditional remove: only evict while the value is still the cooled value we computed. An
        // interleaved TripTopic re-trip changes it, and the remove then leaves the fresh trip in place.
        var removed = ((ICollection<KeyValuePair<RoutingKey, int>>)_trippedTopics)
            .Remove(new KeyValuePair<RoutingKey, int>(topic, cooledValue));

        if (!removed)
            return;

        var span = tracer?.CreateCircuitBreakerSpan(new CircuitBreakerSpanInfo(CircuitBreakerSpanOperation.Reset, topic, 0), instrumentationOptions);
        tracer?.EndSpan(span);

        Log.Reset(s_logger, topic.Value);
    }

    /// <summary>
    /// If a topic exceeds a configurable failure threshold within a time window, mark it as "tripped" 
    /// </summary>
    /// <param name="topic">Name of the entity to circuit break</param>
    public void TripTopic(RoutingKey topic)
    {
        var cooldownCount = _outboxCircuitBreakerOptions.CooldownCount;
        var isReTrip = false;

        _trippedTopics.AddOrUpdate(topic,
            addValueFactory: _ => cooldownCount,
            updateValueFactory: (_, _) =>
            {
                isReTrip = true;
                return cooldownCount;
            });

        var operation = isReTrip ? CircuitBreakerSpanOperation.ReTrip : CircuitBreakerSpanOperation.Trip;
        var span = tracer?.CreateCircuitBreakerSpan(new CircuitBreakerSpanInfo(operation, topic, cooldownCount), instrumentationOptions);
        tracer?.EndSpan(span);

        if (isReTrip)
            Log.ReTripped(s_logger, topic.Value, cooldownCount);
        else
            Log.Tripped(s_logger, topic.Value, cooldownCount);
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Warning, "Circuit breaker tripped for topic {Topic}; suppressing publish for {CooldownCount} cooldown cycle(s)")]
        public static partial void Tripped(ILogger logger, string topic, int cooldownCount);

        [LoggerMessage(LogLevel.Debug, "Circuit breaker re-tripped for topic {Topic}; cooldown extended to {CooldownCount} cycle(s)")]
        public static partial void ReTripped(ILogger logger, string topic, int cooldownCount);

        [LoggerMessage(LogLevel.Information, "Circuit breaker reset for topic {Topic}; publish suppression lifted")]
        public static partial void Reset(ILogger logger, string topic);
    }
}

