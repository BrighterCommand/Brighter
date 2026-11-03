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
using System.Linq;
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
    private readonly TripView _view = new();
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
            return _view.LiveTopics(stamp => IsLive(now, stamp));
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
        lock (_view.Lock)
        {
            _view.Record(topic, stamp);
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
            if (result.LeftEnded || (result.PriorTripTime is not null && HasRunOut(result.StoredTripTime)))
            {
                _view.DropIfNotNewerThan(result.Topic, result.StoredTripTime);
                continue;
            }

            _view.Raise(result.Topic, result.StoredTripTime);

            foreach (var operation in AnnouncementsFor(result))
                Announce(operation, result.Topic);
        }
    }

    private CircuitBreakerSpanOperation[] AnnouncementsFor(TripWriteResult result)
    {
        if (result.PriorTripTime is null)
            return [CircuitBreakerSpanOperation.Trip];

        if (HasRunOut(result.PriorTripTime.Value))
            return [CircuitBreakerSpanOperation.Reset, CircuitBreakerSpanOperation.Trip];

        return [CircuitBreakerSpanOperation.ReTrip];
    }

    private async Task WriteLiveAsync(IReadOnlyCollection<KeyValuePair<RoutingKey, DateTimeOffset>> pending)
    {
        var trips = LiveTrips(pending);
        if (trips.Count == 0)
            return;

        var results = await _store.WriteAsync(trips).ConfigureAwait(false);
        ApplyWriteResults(results);
    }

    private List<TopicTrip> LiveTrips(IReadOnlyCollection<KeyValuePair<RoutingKey, DateTimeOffset>> pending)
    {
        var now = _timeProvider.GetUtcNow();
        var trips = new List<TopicTrip>(pending.Count);
        foreach (var trip in pending)
        {
            if (IsLive(now, trip.Value))
                trips.Add(new TopicTrip(trip.Key, trip.Value));
            else
                _view.DropIfExactly(trip.Key, trip.Value);
        }

        return trips;
    }

    private bool IsLive(DateTimeOffset now, DateTimeOffset stamp) => now - stamp < _tripLength;

    private bool HasRunOut(DateTimeOffset stamp) => !IsLive(_timeProvider.GetUtcNow(), stamp);

    private void Announce(CircuitBreakerSpanOperation operation, RoutingKey topic)
    {
        var span = _tracer?.CreateCircuitBreakerSpan(new CircuitBreakerSpanInfo(operation, topic, 0) { CooldownDuration = _cooldownDuration }, _instrumentationOptions);
        _tracer?.EndSpan(span);

        switch (operation)
        {
            case CircuitBreakerSpanOperation.ReTrip:
                Log.ReTripped(s_logger, topic.Value);
                break;
            case CircuitBreakerSpanOperation.Reset:
                Log.Reset(s_logger, topic.Value);
                break;
            default:
                Log.Tripped(s_logger, topic.Value);
                break;
        }
    }

    private async Task WritePendingAsync(RoutingKey topic)
    {
        var more = true;
        while (more)
        {
            DateTimeOffset stamp;
            lock (_view.Lock)
            {
                stamp = _pendingWrites[topic];
                _pendingWrites.Remove(topic);
            }

            await WriteLiveAsync([new KeyValuePair<RoutingKey, DateTimeOffset>(topic, stamp)]).ConfigureAwait(false);

            lock (_view.Lock)
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
    public async Task<IReadOnlyCollection<RoutingKey>> ReadTrippedTopicsAsync(CancellationToken cancellationToken = default)
    {
        var read = await _store.ReadAsync(_timeProvider.GetUtcNow(), _tripLength, cancellationToken).ConfigureAwait(false);
        MergeLive(read.Live);
        foreach (var ended in read.Ended)
        {
            _view.DropIfNotNewerThan(ended.Topic, ended.TripTime);
            Announce(CircuitBreakerSpanOperation.Reset, ended.Topic);
        }

        return TrippedTopics.ToList();
    }

    private void MergeLive(IReadOnlyCollection<TopicTrip> live)
    {
        lock (_view.Lock)
        {
            foreach (var trip in live)
                _view.Raise(trip.Topic, trip.TripTime);
        }
    }

    /// <inheritdoc />
    public void StartRefresh() => throw new NotImplementedException();

    private sealed class TripBatch(DistributedOutboxCircuitBreaker breaker) : IAmATripBatch
    {
        private readonly Dictionary<RoutingKey, DateTimeOffset> _trips = new();

        public void TripTopic(RoutingKey topic)
        {
            var stamp = breaker.CurrentStamp();
            breaker._view.Record(topic, stamp);
            _trips[topic] = stamp;
        }

        public async Task WriteAsync()
        {
            if (_trips.Count == 0)
                return;

            var pending = _trips.ToArray();
            _trips.Clear();
            await breaker.WriteLiveAsync(pending).ConfigureAwait(false);
        }
    }

    private sealed class TripView
    {
        private readonly Dictionary<RoutingKey, DateTimeOffset> _stamps = new();

        public object Lock { get; } = new();

        public void Record(RoutingKey topic, DateTimeOffset stamp)
        {
            lock (Lock)
                _stamps[topic] = stamp;
        }

        public void Raise(RoutingKey topic, DateTimeOffset stamp)
        {
            lock (Lock)
            {
                if (!_stamps.TryGetValue(topic, out var viewed) || viewed < stamp)
                    _stamps[topic] = stamp;
            }
        }

        public void DropIfNotNewerThan(RoutingKey topic, DateTimeOffset stamp)
        {
            lock (Lock)
            {
                if (_stamps.TryGetValue(topic, out var viewed) && viewed <= stamp)
                    _stamps.Remove(topic);
            }
        }

        public void DropIfExactly(RoutingKey topic, DateTimeOffset stamp)
        {
            lock (Lock)
            {
                if (_stamps.TryGetValue(topic, out var viewed) && viewed == stamp)
                    _stamps.Remove(topic);
            }
        }

        public List<RoutingKey> LiveTopics(Func<DateTimeOffset, bool> isLive)
        {
            lock (Lock)
            {
                var topics = new List<RoutingKey>();
                foreach (var trip in _stamps)
                {
                    if (isLive(trip.Value))
                        topics.Add(trip.Key);
                }

                return topics;
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Warning, "Circuit breaker tripped for topic {Topic}; suppressing publish for the trip length")]
        public static partial void Tripped(ILogger logger, string topic);

        [LoggerMessage(LogLevel.Debug, "Circuit breaker re-tripped for topic {Topic}; trip extended to the stored stamp")]
        public static partial void ReTripped(ILogger logger, string topic);

        [LoggerMessage(LogLevel.Information, "Circuit breaker reset for topic {Topic} (trip length elapsed); publish suppression lifted")]
        public static partial void Reset(ILogger logger, string topic);
    }
}
