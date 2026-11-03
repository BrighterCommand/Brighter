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
    private static readonly TimeSpan s_shortestPermittedCooldown = TimeSpan.FromTicks(1);
    private static readonly TimeSpan s_shortestPermittedLeeway = TimeSpan.Zero;
    private static readonly TimeSpan s_longestPermittedDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan s_shortestPermittedStoreTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan s_longestPermittedStoreTimeout = TimeSpan.FromSeconds(60);
    private const string TimeoutFailure = "timeout";
    private const int InFlightGuardTimeouts = 4;
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
    private readonly Dictionary<RoutingKey, PendingTrip> _pendingWrites = new();
    private readonly HashSet<RoutingKey> _writesInFlight = new();
    private readonly InFlightGuard _readGuard = new();
    private readonly InFlightGuard _refreshGuard = new();
    private readonly InFlightGuard _recoveryGuard = new();
    private readonly HashSet<RoutingKey> _unsharedTopics = new();
    private long _readsStarted;
    private long _newestAppliedRead;
    private bool _isReadFailing;
    private string? _lastFailure;

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
        _cooldownDuration = RequireWithin(options.CooldownDuration, s_shortestPermittedCooldown, s_longestPermittedDuration, nameof(options.CooldownDuration));
        _clockSkewLeeway = RequireWithin(options.ClockSkewLeeway, s_shortestPermittedLeeway, s_longestPermittedDuration, nameof(options.ClockSkewLeeway));
        _storeTimeout = RequireWithin(options.StoreTimeout, s_shortestPermittedStoreTimeout, s_longestPermittedStoreTimeout, nameof(options.StoreTimeout));
        _tripLength = _cooldownDuration + _clockSkewLeeway;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _tracer = tracer;
        _instrumentationOptions = instrumentationOptions;
    }

    private static TimeSpan RequireWithin(TimeSpan value, TimeSpan min, TimeSpan max, string optionName)
    {
        if (value < min || value > max)
            throw new ArgumentOutOfRangeException("options", value,
                $"{optionName} must be at least {min} and at most {max}");

        return value;
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
    public TrippedTopicStoreStatus StoreStatus
    {
        get
        {
            lock (_view.Lock)
            {
                ClearFailureWhenFallbackEnds();
                return new TrippedTopicStoreStatus(IsFallbackActive(), _lastFailure);
            }
        }
    }

    private bool IsFallbackActive() => _isReadFailing || _unsharedTopics.Count > 0;

    private void ClearFailureWhenFallbackEnds()
    {
        var now = _timeProvider.GetUtcNow();
        _unsharedTopics.RemoveWhere(topic => !_view.IsLive(topic, stamp => IsLive(now, stamp)));
        if (!IsFallbackActive())
            _lastFailure = null;
    }

    private void RecordTimeout()
    {
        lock (_view.Lock)
            _lastFailure = TimeoutFailure;
    }

    private void RecordReadTimeout()
    {
        lock (_view.Lock)
            _isReadFailing = true;
    }

    private void RecordWriteTimeout(IEnumerable<RoutingKey> topics)
    {
        lock (_view.Lock)
            _unsharedTopics.UnionWith(topics);
    }

    private void RecordWriteFailure(Exception failure, IEnumerable<RoutingKey> topics)
    {
        lock (_view.Lock)
        {
            _unsharedTopics.UnionWith(topics);
            _lastFailure = failure.GetType().Name;
        }

        Log.WriteFailed(s_logger, failure, failure.GetType().Name);
    }

    private void RecordWriteSuccess(RoutingKey topic)
    {
        lock (_view.Lock)
        {
            _unsharedTopics.Remove(topic);
            ClearFailureWhenFallbackEnds();
        }
    }

    private void RecordReadFailure(Exception failure)
    {
        lock (_view.Lock)
        {
            _isReadFailing = true;
            _lastFailure = failure.GetType().Name;
        }

        Log.ReadFailed(s_logger, failure, failure.GetType().Name);
    }

    private void RecordReadSuccess()
    {
        lock (_view.Lock)
        {
            _isReadFailing = false;
            ClearFailureWhenFallbackEnds();
        }
    }

    private async Task SettleAsync(Task call)
    {
        try
        {
            await call.WaitAsync(TimeSpan.FromTicks(_storeTimeout.Ticks * InFlightGuardTimeouts), _timeProvider).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Log.InFlightGuardReleased(s_logger, _storeTimeout.TotalSeconds * InFlightGuardTimeouts);
        }
        catch (Exception)
        {
            return;
        }
    }

    private static Task<T> StartCall<T>(Func<CancellationToken, Task<T>> call, CancellationToken token, Action<Task> holdGuardUntil)
    {
        try
        {
            return call(token);
        }
        catch
        {
            holdGuardUntil(Task.CompletedTask);
            throw;
        }
    }

    private async Task<T?> WithinStoreTimeoutAsync<T>(Func<CancellationToken, Task<T>> call, CancellationToken cancellationToken, Action<Task> holdGuardUntil) where T : class
    {
        using var timeout = _timeProvider.CreateCancellationTokenSource(_storeTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var task = StartCall(call, linked.Token, holdGuardUntil);
        _ = task.ContinueWith(abandoned => _ = abandoned.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        holdGuardUntil(SettleAsync(task));

        try
        {
            return await task.WaitAsync(_storeTimeout, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is TimeoutException || (e is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            RecordTimeout();
            return null;
        }
    }

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
            _pendingWrites[topic] = RecordTrip(topic, stamp, _pendingWrites.TryGetValue(topic, out var earlier) ? earlier : null);
            startWrite = _writesInFlight.Add(topic);
        }

        if (startWrite)
            _ = Task.Run(() => WritePendingAsync(topic));
    }

    private PendingTrip RecordTrip(RoutingKey topic, DateTimeOffset stamp, PendingTrip? earlier)
    {
        var prior = _view.Record(topic, stamp);
        var replacesLiveTrip = earlier?.ReplacesLiveTrip ?? (prior is not null && IsLive(_timeProvider.GetUtcNow(), prior.Value));
        return new PendingTrip(stamp, replacesLiveTrip);
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
            RecordWriteSuccess(result.Topic);
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

    private async Task WriteLiveAsync(IReadOnlyCollection<KeyValuePair<RoutingKey, PendingTrip>> pending, Action<Task> holdGuardUntil)
    {
        var live = LivePending(pending);
        var trips = live.Select(trip => new TopicTrip(trip.Key, trip.Value.Stamp)).ToList();
        trips.AddRange(UnsharedTripsExcluding(live.Select(trip => trip.Key)));
        if (trips.Count == 0)
            return;

        IReadOnlyList<TripWriteResult>? results;
        try
        {
            results = await WithinStoreTimeoutAsync(_ => _store.WriteAsync(trips), CancellationToken.None, holdGuardUntil).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            RecordWriteFailure(e, trips.Select(trip => trip.Topic));
            AnnounceUnshared(live);
            return;
        }

        if (results is null)
            RecordWriteTimeout(trips.Select(trip => trip.Topic));
        else
            ApplyWriteResults(results);
    }

    private List<TopicTrip> UnsharedTripsExcluding(IEnumerable<RoutingKey> tripped)
    {
        var now = _timeProvider.GetUtcNow();
        lock (_view.Lock)
        {
            var carried = new List<TopicTrip>();
            foreach (var topic in _unsharedTopics.Except(tripped))
            {
                var stamp = _view.StampOf(topic);
                if (stamp is not null && IsLive(now, stamp.Value))
                    carried.Add(new TopicTrip(topic, stamp.Value));
            }

            return carried;
        }
    }

    private void AnnounceUnshared(IEnumerable<KeyValuePair<RoutingKey, PendingTrip>> unshared)
    {
        foreach (var trip in unshared)
            Announce(trip.Value.ReplacesLiveTrip ? CircuitBreakerSpanOperation.ReTrip : CircuitBreakerSpanOperation.Trip, trip.Key);
    }

    private List<KeyValuePair<RoutingKey, PendingTrip>> LivePending(IReadOnlyCollection<KeyValuePair<RoutingKey, PendingTrip>> pending)
    {
        var now = _timeProvider.GetUtcNow();
        var live = new List<KeyValuePair<RoutingKey, PendingTrip>>(pending.Count);
        foreach (var trip in pending)
        {
            if (IsLive(now, trip.Value.Stamp))
                live.Add(trip);
            else
                _view.DropIfExactly(trip.Key, trip.Value.Stamp);
        }

        return live;
    }

    private bool IsLive(DateTimeOffset now, DateTimeOffset stamp) => now - stamp < _tripLength;

    private bool HasRunOut(DateTimeOffset stamp) => !IsLive(_timeProvider.GetUtcNow(), stamp);

    private CircuitBreakerSpanInfo CreateSpanInfo(CircuitBreakerSpanOperation operation, RoutingKey topic)
    {
        const int noCooldownCount = 0;
        return new CircuitBreakerSpanInfo(operation, topic, noCooldownCount) { CooldownDuration = _cooldownDuration };
    }

    private void Announce(CircuitBreakerSpanOperation operation, RoutingKey topic)
    {
        var span = _tracer?.CreateCircuitBreakerSpan(CreateSpanInfo(operation, topic), _instrumentationOptions);
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
        try
        {
            while (await WriteNextPendingAsync(topic).ConfigureAwait(false))
            {
            }
        }
        finally
        {
            lock (_view.Lock)
                _writesInFlight.Remove(topic);
        }
    }

    private async Task<bool> WriteNextPendingAsync(RoutingKey topic)
    {
        PendingTrip pending;
        lock (_view.Lock)
        {
            pending = _pendingWrites[topic];
            _pendingWrites.Remove(topic);
        }

        var settled = Task.CompletedTask;
        try
        {
            await WriteLiveAsync([new KeyValuePair<RoutingKey, PendingTrip>(topic, pending)], task => settled = task).ConfigureAwait(false);
        }
        finally
        {
            await settled.ConfigureAwait(false);
        }

        lock (_view.Lock)
            return _pendingWrites.ContainsKey(topic);
    }

    /// <inheritdoc />
    public IAmATripBatch BeginTripBatch() => new TripBatch(this);

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<RoutingKey>> ReadTrippedTopicsAsync(CancellationToken cancellationToken = default)
    {
        await MergeStoreIntoViewAsync(_readGuard, cancellationToken).ConfigureAwait(false);
        return TrippedTopics.ToList();
    }

    private async Task MergeStoreIntoViewAsync(InFlightGuard guard, CancellationToken cancellationToken)
    {
        if (!guard.TryEnter())
            return;

        var readNumber = Interlocked.Increment(ref _readsStarted);
        TrippedTopicStoreRead? read;
        try
        {
            read = await WithinStoreTimeoutAsync(
                token => _store.ReadAsync(_timeProvider.GetUtcNow(), _tripLength, token), cancellationToken,
                settled => _ = guard.ReleaseAfterAsync(settled)).ConfigureAwait(false);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            if (!IsStale(readNumber))
                RecordReadFailure(e);
            return;
        }

        if (read is null)
        {
            if (!IsStale(readNumber))
                RecordReadTimeout();
            return;
        }

        var isStale = TryApply(readNumber);
        if (!isStale)
        {
            RecordReadSuccess();
            MergeLive(read.Live);
            StartRecoveryWrite();
        }

        foreach (var ended in read.Ended)
        {
            _view.DropIfNotNewerThan(ended.Topic, ended.TripTime);
            Announce(CircuitBreakerSpanOperation.Reset, ended.Topic);
        }
    }

    private void StartRecoveryWrite()
    {
        lock (_view.Lock)
        {
            if (_unsharedTopics.Count == 0)
                return;
        }

        if (_recoveryGuard.TryEnter())
            _ = Task.Run(RecoverUnsharedAsync);
    }

    private async Task RecoverUnsharedAsync()
    {
        var settled = Task.CompletedTask;
        try
        {
            await WriteLiveAsync([], task => settled = task).ConfigureAwait(false);
        }
        finally
        {
            await _recoveryGuard.ReleaseAfterAsync(settled).ConfigureAwait(false);
        }
    }

    private bool IsStale(long readNumber)
    {
        lock (_view.Lock)
            return readNumber < _newestAppliedRead;
    }

    private bool TryApply(long readNumber)
    {
        lock (_view.Lock)
        {
            if (readNumber < _newestAppliedRead)
                return true;

            _newestAppliedRead = readNumber;
            return false;
        }
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
    public void StartRefresh() => _ = Task.Run(RefreshAsync);

    private async Task RefreshAsync()
    {
        try
        {
            await MergeStoreIntoViewAsync(_refreshGuard, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            RecordReadFailure(e);
        }
    }

    private sealed class InFlightGuard
    {
        private readonly object _lock = new();
        private bool _isInFlight;

        public bool TryEnter()
        {
            lock (_lock)
            {
                if (_isInFlight)
                    return false;

                _isInFlight = true;
                return true;
            }
        }

        public async Task ReleaseAfterAsync(Task settled)
        {
            try
            {
                await settled.ConfigureAwait(false);
            }
            finally
            {
                lock (_lock)
                    _isInFlight = false;
            }
        }
    }

    private sealed class TripBatch(DistributedOutboxCircuitBreaker breaker) : IAmATripBatch
    {
        private readonly Dictionary<RoutingKey, PendingTrip> _trips = new();

        public void TripTopic(RoutingKey topic)
        {
            var stamp = breaker.CurrentStamp();
            _trips[topic] = breaker.RecordTrip(topic, stamp, _trips.TryGetValue(topic, out var earlier) ? earlier : null);
        }

        public async Task WriteAsync()
        {
            if (_trips.Count == 0)
                return;

            var pending = _trips.ToArray();
            _trips.Clear();
            await breaker.WriteLiveAsync(pending, _ => { }).ConfigureAwait(false);
        }
    }

    private readonly record struct PendingTrip(DateTimeOffset Stamp, bool ReplacesLiveTrip);

    private sealed class TripView
    {
        private readonly Dictionary<RoutingKey, DateTimeOffset> _stamps = new();

        public object Lock { get; } = new();

        public DateTimeOffset? Record(RoutingKey topic, DateTimeOffset stamp)
        {
            lock (Lock)
            {
                DateTimeOffset? prior = _stamps.TryGetValue(topic, out var viewed) ? viewed : null;
                _stamps[topic] = stamp;
                return prior;
            }
        }

        public void Raise(RoutingKey topic, DateTimeOffset stamp)
        {
            lock (Lock)
            {
                if (!_stamps.TryGetValue(topic, out var viewed) || viewed < stamp)
                    _stamps[topic] = stamp;
            }
        }

        public DateTimeOffset? StampOf(RoutingKey topic)
        {
            lock (Lock)
                return _stamps.TryGetValue(topic, out var stamp) ? stamp : null;
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

        public bool IsLive(RoutingKey topic, Func<DateTimeOffset, bool> isLive)
        {
            lock (Lock)
                return _stamps.TryGetValue(topic, out var stamp) && isLive(stamp);
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

        [LoggerMessage(LogLevel.Warning, "Reading tripped topics from the store failed with {FailureType}; using this instance's own view")]
        public static partial void ReadFailed(ILogger logger, Exception exception, string failureType);

        [LoggerMessage(LogLevel.Warning, "Writing trips to the store failed with {FailureType}; the trips stay unshared on this instance")]
        public static partial void WriteFailed(ILogger logger, Exception exception, string failureType);

        [LoggerMessage(LogLevel.Warning, "A store call is still running after {Seconds} seconds; no longer holding its in-flight guard")]
        public static partial void InFlightGuardReleased(ILogger logger, double seconds);
    }
}
