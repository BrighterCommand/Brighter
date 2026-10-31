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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.CircuitBreaker;

/// <summary>
/// An outbox circuit breaker that shares its trips between instances through an <see cref="IAmATrippedTopicStore"/>.
/// </summary>
public partial class DistributedOutboxCircuitBreaker : IAmAnOutboxCircuitBreaker, IAmAnOutboxCircuitBreakerAsync, IAmASharedOutboxCircuitBreaker
{
    private static readonly TimeSpan s_longestPermittedDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan s_shortestPermittedStoreTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan s_longestPermittedStoreTimeout = TimeSpan.FromSeconds(60);
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<DistributedOutboxCircuitBreaker>();

    private readonly IAmATrippedTopicStore _store;
    private readonly TimeSpan _cooldownDuration;
    private readonly TimeSpan _clockSkewLeeway;
    private readonly TimeSpan _storeTimeout;
    private readonly TimeSpan _tripLength;
    private readonly TimeProvider _timeProvider;
    private readonly IAmABrighterTracer? _tracer;
    private readonly InstrumentationOptions _instrumentationOptions;
    private readonly object _lock = new();
    private readonly Dictionary<RoutingKey, DateTimeOffset> _view = new();
    private readonly Dictionary<RoutingKey, DateTimeOffset> _pendingWrites = new();
    private readonly HashSet<RoutingKey> _writesInFlight = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedOutboxCircuitBreaker"/> class.
    /// </summary>
    /// <param name="store">The store that shares trips between instances</param>
    /// <param name="options">The cooldown duration, clock skew leeway and store timeout; defaults are used when null</param>
    /// <param name="tracer">Optional tracer used to export a span for each trip/re-trip/reset transition</param>
    /// <param name="instrumentationOptions">How verbose should the exported spans be</param>
    /// <param name="timeProvider">The clock used to timestamp trips; defaults to <see cref="TimeProvider.System"/></param>
    /// <exception cref="ArgumentNullException">Thrown when the store is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when CooldownDuration is not greater than zero and at most 24 hours,
    /// ClockSkewLeeway is negative or over 24 hours, or StoreTimeout is under 1 second or over 60 seconds</exception>
    public DistributedOutboxCircuitBreaker(
        IAmATrippedTopicStore store,
        OutboxCircuitBreakerOptions? options = null,
        IAmABrighterTracer? tracer = null,
        InstrumentationOptions instrumentationOptions = InstrumentationOptions.All,
        TimeProvider? timeProvider = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        options ??= new OutboxCircuitBreakerOptions();
        _cooldownDuration = ValidatedCooldownDuration(options);
        _clockSkewLeeway = ValidatedClockSkewLeeway(options);
        _storeTimeout = ValidatedStoreTimeout(options);
        _tripLength = _cooldownDuration + _clockSkewLeeway;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _tracer = tracer;
        _instrumentationOptions = instrumentationOptions;
    }

    private static TimeSpan ValidatedCooldownDuration(OutboxCircuitBreakerOptions options)
    {
        var cooldownDuration = options.CooldownDuration;
        if (cooldownDuration <= TimeSpan.Zero || cooldownDuration > s_longestPermittedDuration)
            throw new ArgumentOutOfRangeException(nameof(options), cooldownDuration,
                "CooldownDuration must be greater than zero and at most 24 hours");

        return cooldownDuration;
    }

    private static TimeSpan ValidatedClockSkewLeeway(OutboxCircuitBreakerOptions options)
    {
        var clockSkewLeeway = options.ClockSkewLeeway;
        if (clockSkewLeeway < TimeSpan.Zero || clockSkewLeeway > s_longestPermittedDuration)
            throw new ArgumentOutOfRangeException(nameof(options), clockSkewLeeway,
                "ClockSkewLeeway must not be negative and at most 24 hours");

        return clockSkewLeeway;
    }

    private static TimeSpan ValidatedStoreTimeout(OutboxCircuitBreakerOptions options)
    {
        var storeTimeout = options.StoreTimeout;
        if (storeTimeout < s_shortestPermittedStoreTimeout || storeTimeout > s_longestPermittedStoreTimeout)
            throw new ArgumentOutOfRangeException(nameof(options), storeTimeout,
                "StoreTimeout must be at least 1 second and at most 60 seconds");

        return storeTimeout;
    }

    /// <inheritdoc />
    public IEnumerable<RoutingKey> TrippedTopics
    {
        get
        {
            var now = _timeProvider.GetUtcNow();
            lock (_lock)
            {
                var topics = new List<RoutingKey>();
                foreach (var trip in _view)
                {
                    if (now - trip.Value < _tripLength)
                        topics.Add(trip.Key);
                }

                return topics;
            }
        }
    }

    /// <inheritdoc />
    public TrippedTopicStoreStatus StoreStatus => throw new NotImplementedException();

    /// <inheritdoc />
    public void CoolDown()
    {
    }

    /// <inheritdoc />
    public void TripTopic(RoutingKey topic)
    {
        var stamp = CurrentStamp();
        bool startWrite;
        lock (_lock)
        {
            _view[topic] = stamp;
            _pendingWrites[topic] = stamp;
            startWrite = _writesInFlight.Add(topic);
        }

        if (startWrite)
            _ = Task.Run(() => WritePendingAsync(topic));
    }

    private DateTimeOffset CurrentStamp()
    {
        var now = _timeProvider.GetUtcNow();
        return now.AddTicks(-(now.Ticks % 10));
    }

    private void ApplyWriteResults(IReadOnlyList<TripWriteResult> results)
    {
        foreach (var result in results)
        {
            if (result.LeftEnded)
                continue;

            lock (_lock)
            {
                if (!_view.TryGetValue(result.Topic, out var viewed) || viewed < result.StoredTripTime)
                    _view[result.Topic] = result.StoredTripTime;
            }

            Announce(result.PriorTripTime is null ? CircuitBreakerSpanOperation.Trip : CircuitBreakerSpanOperation.ReTrip, result.Topic);
        }
    }

    private void Announce(CircuitBreakerSpanOperation operation, RoutingKey topic)
    {
        var span = _tracer?.CreateCircuitBreakerSpan(new CircuitBreakerSpanInfo(operation, topic, 0), _instrumentationOptions);
        _tracer?.EndSpan(span);

        if (operation == CircuitBreakerSpanOperation.ReTrip)
            Log.ReTripped(s_logger, topic.Value);
        else
            Log.Tripped(s_logger, topic.Value);
    }

    private async Task WritePendingAsync(RoutingKey topic)
    {
        var more = true;
        while (more)
        {
            DateTimeOffset stamp;
            lock (_lock)
            {
                stamp = _pendingWrites[topic];
                _pendingWrites.Remove(topic);
            }

            await _store.WriteAsync([new TopicTrip(topic, stamp)]).ConfigureAwait(false);

            lock (_lock)
            {
                more = _pendingWrites.ContainsKey(topic);
                if (!more)
                    _writesInFlight.Remove(topic);
            }
        }
    }

    /// <inheritdoc />
    public IAmATripBatch BeginTripBatch() => new TripBatch(this);

    /// <inheritdoc />
    public Task<IReadOnlyCollection<RoutingKey>> ReadTrippedTopicsAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public void StartRefresh() => throw new NotImplementedException();

    private sealed class TripBatch(DistributedOutboxCircuitBreaker breaker) : IAmATripBatch
    {
        private readonly Dictionary<RoutingKey, DateTimeOffset> _trips = new();

        public void TripTopic(RoutingKey topic)
        {
            var stamp = breaker.CurrentStamp();
            lock (breaker._lock)
            {
                breaker._view[topic] = stamp;
            }

            _trips[topic] = stamp;
        }

        public async Task WriteAsync()
        {
            if (_trips.Count == 0)
                return;

            var trips = new List<TopicTrip>(_trips.Count);
            foreach (var trip in _trips)
                trips.Add(new TopicTrip(trip.Key, trip.Value));
            _trips.Clear();

            var results = await breaker._store.WriteAsync(trips).ConfigureAwait(false);
            breaker.ApplyWriteResults(results);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Warning, "Circuit breaker tripped for topic {Topic}; suppressing publish for the trip length")]
        public static partial void Tripped(ILogger logger, string topic);

        [LoggerMessage(LogLevel.Debug, "Circuit breaker re-tripped for topic {Topic}; trip extended to the stored stamp")]
        public static partial void ReTripped(ILogger logger, string topic);
    }
}
