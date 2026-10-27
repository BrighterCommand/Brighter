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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
/// <param name="timeProvider">The clock used to timestamp trips; defaults to <see cref="TimeProvider.System"/></param>
public partial class InMemoryOutboxCircuitBreaker(
    OutboxCircuitBreakerOptions? options = null,
    IAmABrighterTracer? tracer = null,
    InstrumentationOptions instrumentationOptions = InstrumentationOptions.All,
    TimeProvider? timeProvider = null) : IAmAnOutboxCircuitBreaker
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<InMemoryOutboxCircuitBreaker>();

    private readonly OutboxCircuitBreakerOptions _outboxCircuitBreakerOptions = options ?? new OutboxCircuitBreakerOptions();

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    private readonly ConcurrentDictionary<RoutingKey, TripEntry> _trippedTopics = new();

    private sealed record TripEntry(int Count, long Timestamp)
    {
        public bool Equals(TripEntry? other) => ReferenceEquals(this, other);

        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }

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
            if (TryDecrement(trippedTopicsKey, out var cooled))
                TryEvictAndAnnounceReset(trippedTopicsKey, cooled);
        }
    }

    private bool TryDecrement(RoutingKey topic, out TripEntry cooled)
    {
        while (_trippedTopics.TryGetValue(topic, out var observed))
        {
            cooled = observed with { Count = observed.Count - 1 };
            if (_trippedTopics.TryUpdate(topic, cooled, observed))
                return true;
        }

        cooled = null!;
        return false;
    }

    private void TryEvictAndAnnounceReset(RoutingKey topic, TripEntry cooled)
    {
        if (cooled.Count >= 0)
            return;

        var removed = ((ICollection<KeyValuePair<RoutingKey, TripEntry>>)_trippedTopics)
            .Remove(new KeyValuePair<RoutingKey, TripEntry>(topic, cooled));

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

        var entry = new TripEntry(cooldownCount, _timeProvider.GetTimestamp());

        _trippedTopics.AddOrUpdate(topic,
            addValueFactory: _ => entry,
            updateValueFactory: (_, _) =>
            {
                isReTrip = true;
                return entry;
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

