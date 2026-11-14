#region License

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
    private readonly FallbackState _fallback;
    private long _readsStarted;
    private long _newestAppliedRead;

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
        _fallback = new FallbackState(_view.Lock, () => _timeProvider.GetUtcNow(), HasLiveTripAt);
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
    public TrippedTopicStoreStatus StoreStatus => _fallback.Status();

    private bool HasLiveTripAt(RoutingKey topic, DateTimeOffset now) => _view.IsLive(topic, stamp => IsLive(now, stamp));

    private void AnnounceEndedUnsharedTrips()
    {
        foreach (var topic in _fallback.TakeEndedUnshared())
            Announce(CircuitBreakerSpanOperation.Reset, topic);
    }

    private void RecordWriteFailure(Exception failure, IEnumerable<RoutingKey> topics)
    {
        _fallback.RecordWriteFailure(failure, topics);
        Log.WriteFailed(s_logger, failure, failure.GetType().Name);
    }

    private void RecordWriteTimeout(IEnumerable<RoutingKey> topics, IEnumerable<KeyValuePair<RoutingKey, PendingTrip>> live)
    {
        _fallback.RecordWriteTimeout(topics);
        Log.WriteTimedOut(s_logger);
        AnnounceUnshared(live);
    }

    private void RecordReadTimeout()
    {
        _fallback.RecordReadTimeout();
        Log.ReadTimedOut(s_logger);
    }

    private void RecordReadFailure(Exception failure)
    {
        _fallback.RecordReadFailure(failure);
        Log.ReadFailed(s_logger, failure, failure.GetType().Name);
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

    private StoreCall<T> BeginStoreCall<T>(Func<CancellationToken, Task<T>> call, CancellationToken cancellationToken) where T : class
    {
        var timeout = _timeProvider.CreateCancellationTokenSource(_storeTimeout);
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        Task<T> task;
        try
        {
            task = call(linked.Token);
        }
        catch (Exception e)
        {
            linked.Dispose();
            timeout.Dispose();
            return new StoreCall<T>(Task.CompletedTask, Task.FromException<T?>(e));
        }

        _ = task.ContinueWith(abandoned => _ = abandoned.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        return new StoreCall<T>(SettleAsync(task), WithinStoreTimeoutAsync(task, timeout, linked, cancellationToken));
    }

    private async Task<T?> WithinStoreTimeoutAsync<T>(Task<T> task, CancellationTokenSource timeout, CancellationTokenSource linked, CancellationToken cancellationToken) where T : class
    {
        using var ownedTimeout = timeout;
        using var ownedLinked = linked;

        try
        {
            return await task.WaitAsync(_storeTimeout, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is TimeoutException || (e is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _fallback.RecordTimeout();
            return null;
        }
    }

    /// <inheritdoc />
    public void CoolDown() => AnnounceEndedUnsharedTrips();

    /// <inheritdoc />
    public void TripTopic(RoutingKey topic)
    {
        var stamp = StampNewTrip();
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

    private DateTimeOffset StampNewTrip()
    {
        AnnounceEndedUnsharedTrips();
        return CurrentStamp();
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
            _fallback.RecordWriteSuccess(result.Topic);
            var alreadyAnnounced = _fallback.TakeAnnouncedFromView(result.Topic);
            if (result.LeftEnded || (result.PriorTripTime is not null && HasRunOut(result.StoredTripTime)))
            {
                _view.DropIfNotNewerThan(result.Topic, result.StoredTripTime);
                continue;
            }

            _view.Raise(result.Topic, result.StoredTripTime);

            foreach (var operation in AnnouncementsFor(result, alreadyAnnounced))
                Announce(operation, result.Topic);
        }
    }

    private CircuitBreakerSpanOperation[] AnnouncementsFor(TripWriteResult result, bool alreadyAnnounced)
    {
        if (result.PriorTripTime is null)
            return alreadyAnnounced ? [] : [CircuitBreakerSpanOperation.Trip];

        if (HasRunOut(result.PriorTripTime.Value))
            return alreadyAnnounced
                ? [CircuitBreakerSpanOperation.Reset]
                : [CircuitBreakerSpanOperation.Reset, CircuitBreakerSpanOperation.Trip];

        return alreadyAnnounced ? [] : [CircuitBreakerSpanOperation.ReTrip];
    }

    private async Task<Task> WriteLiveAsync(IReadOnlyCollection<KeyValuePair<RoutingKey, PendingTrip>> pending)
    {
        var live = LivePending(pending);
        var trips = live.Select(trip => new TopicTrip(trip.Key, trip.Value.Stamp)).ToList();
        trips.AddRange(UnsharedTripsExcluding(live.Select(trip => trip.Key)));
        if (trips.Count == 0)
            return Task.CompletedTask;

        var settled = Task.CompletedTask;
        IReadOnlyList<TripWriteResult>? results;
        try
        {
            var storeCall = BeginStoreCall(token => _store.WriteAsync(trips, token), CancellationToken.None);
            settled = storeCall.Settled;
            results = await storeCall.Result.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            RecordWriteFailure(e, trips.Select(trip => trip.Topic));
            AnnounceUnshared(live);
            return settled;
        }

        if (results is null)
            RecordWriteTimeout(trips.Select(trip => trip.Topic), live);
        else
            ApplyWriteResults(results);

        return settled;
    }

    private List<TopicTrip> UnsharedTripsExcluding(IEnumerable<RoutingKey> tripped)
    {
        var now = _timeProvider.GetUtcNow();
        lock (_view.Lock)
        {
            var carried = new List<TopicTrip>();
            foreach (var topic in _fallback.UnsharedTopicsExcluding(tripped))
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
        {
            _fallback.MarkAnnounced(trip.Key);
            Announce(trip.Value.ReplacesLiveTrip ? CircuitBreakerSpanOperation.ReTrip : CircuitBreakerSpanOperation.Trip, trip.Key);
        }
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
            settled = await WriteLiveAsync([new KeyValuePair<RoutingKey, PendingTrip>(topic, pending)]).ConfigureAwait(false);
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
        AnnounceEndedUnsharedTrips();
        await MergeStoreIntoViewAsync(_readGuard, cancellationToken).ConfigureAwait(false);
        return TrippedTopics.ToList();
    }

    private async Task MergeStoreIntoViewAsync(InFlightGuard guard, CancellationToken cancellationToken)
    {
        if (!guard.TryEnter())
            return;

        var readNumber = Interlocked.Increment(ref _readsStarted);
        var read = new ExpiredThenLiveRead(_store, _timeProvider, _tripLength);
        await ApplyReadOutcomeAsync(read, readNumber, guard, cancellationToken).ConfigureAwait(false);
        EndRowsFrom(read.Ended);
    }

    private async Task ApplyReadOutcomeAsync(ExpiredThenLiveRead read, long readNumber, InFlightGuard guard, CancellationToken cancellationToken)
    {
        IReadOnlyList<TopicTrip>? live;
        try
        {
            var storeCall = BeginStoreCall(read.ReadAsync, cancellationToken);
            _ = guard.ReleaseAfterAsync(storeCall.Settled);
            live = await storeCall.Result.ConfigureAwait(false);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            if (!IsStale(readNumber))
                RecordReadFailure(e);
            return;
        }

        if (live is null)
        {
            if (!IsStale(readNumber))
                RecordReadTimeout();
            return;
        }

        ApplyRead(live, readNumber);
    }

    private void ApplyRead(IReadOnlyCollection<TopicTrip> live, long readNumber)
    {
        if (!TryClaim(readNumber))
            return;

        _fallback.RecordReadSuccess();
        MergeLive(live);
        StartRecoveryWrite();
    }

    private void EndRowsFrom(IReadOnlyCollection<TopicTrip> ended)
    {
        foreach (var trip in ended)
        {
            _view.DropIfNotNewerThan(trip.Topic, trip.TripTime);
            Announce(CircuitBreakerSpanOperation.Reset, trip.Topic);
        }
    }

    private void StartRecoveryWrite()
    {
        if (!_fallback.HasUnsharedTopics())
            return;

        if (_recoveryGuard.TryEnter())
            _ = Task.Run(RecoverUnsharedAsync);
    }

    private async Task RecoverUnsharedAsync()
    {
        var settled = Task.CompletedTask;
        try
        {
            settled = await WriteLiveAsync([]).ConfigureAwait(false);
        }
        finally
        {
            await _recoveryGuard.ReleaseAfterAsync(settled).ConfigureAwait(false);
        }
    }

    private bool IsStale(long readNumber)
    {
        lock (_view.Lock)
            return IsSuperseded(readNumber);
    }

    private bool TryClaim(long readNumber)
    {
        lock (_view.Lock)
        {
            if (IsSuperseded(readNumber))
                return false;

            _newestAppliedRead = readNumber;
            return true;
        }
    }

    private bool IsSuperseded(long readNumber) => readNumber < _newestAppliedRead;

    private void MergeLive(IReadOnlyCollection<TopicTrip> live)
    {
        lock (_view.Lock)
        {
            foreach (var trip in live)
                _view.Raise(trip.Topic, trip.TripTime);
        }
    }

    /// <inheritdoc />
    public void StartRefresh()
    {
        AnnounceEndedUnsharedTrips();
        _ = Task.Run(RefreshAsync);
    }

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
            var stamp = breaker.StampNewTrip();
            _trips[topic] = breaker.RecordTrip(topic, stamp, _trips.TryGetValue(topic, out var earlier) ? earlier : null);
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

    private readonly record struct PendingTrip(DateTimeOffset Stamp, bool ReplacesLiveTrip);

    private sealed class ExpiredThenLiveRead(IAmATrippedTopicStore store, TimeProvider timeProvider, TimeSpan tripLength)
    {
        private volatile IReadOnlyList<TopicTrip> _ended = [];

        public IReadOnlyList<TopicTrip> Ended => _ended;

        public async Task<IReadOnlyList<TopicTrip>> ReadAsync(CancellationToken token)
        {
            var window = new TripWindow(timeProvider.GetUtcNow(), tripLength);
            _ended = await store.EndExpiredAsync(window, token).ConfigureAwait(false);
            return await store.ReadLiveAsync(window, token).ConfigureAwait(false);
        }
    }

    private readonly record struct StoreCall<T>(Task Settled, Task<T?> Result) where T : class;

    private sealed class FallbackState(object gate, Func<DateTimeOffset> now, Func<RoutingKey, DateTimeOffset, bool> hasLiveTripAt)
    {
        private const string TimeoutFailure = "timeout";

        private readonly HashSet<RoutingKey> _unsharedTopics = new();
        private readonly HashSet<RoutingKey> _announcedUnshared = new();
        private readonly HashSet<RoutingKey> _endedUnsharedToAnnounce = new();
        private bool _isReadFailing;
        private string? _lastFailure;

        public TrippedTopicStoreStatus Status()
        {
            lock (gate)
            {
                ClearFailureWhenFallbackEnds();
                return new TrippedTopicStoreStatus(IsFallbackActive(), _lastFailure);
            }
        }

        public List<RoutingKey> TakeEndedUnshared()
        {
            lock (gate)
            {
                ClearFailureWhenFallbackEnds();
                var ended = _endedUnsharedToAnnounce.ToList();
                _endedUnsharedToAnnounce.Clear();
                return ended;
            }
        }

        public bool TakeAnnouncedFromView(RoutingKey topic)
        {
            lock (gate)
                return _announcedUnshared.Remove(topic);
        }

        public void MarkAnnounced(RoutingKey topic)
        {
            lock (gate)
                _announcedUnshared.Add(topic);
        }

        public bool HasUnsharedTopics()
        {
            lock (gate)
                return _unsharedTopics.Count > 0;
        }

        public List<RoutingKey> UnsharedTopicsExcluding(IEnumerable<RoutingKey> tripped)
        {
            lock (gate)
                return _unsharedTopics.Except(tripped).ToList();
        }

        public void RecordTimeout()
        {
            lock (gate)
                _lastFailure = TimeoutFailure;
        }

        public void RecordReadTimeout()
        {
            lock (gate)
                _isReadFailing = true;
        }

        public void RecordWriteTimeout(IEnumerable<RoutingKey> topics)
        {
            lock (gate)
                _unsharedTopics.UnionWith(topics);
        }

        public void RecordWriteFailure(Exception failure, IEnumerable<RoutingKey> topics)
        {
            lock (gate)
            {
                _unsharedTopics.UnionWith(topics);
                _lastFailure = failure.GetType().Name;
            }
        }

        public void RecordWriteSuccess(RoutingKey topic)
        {
            lock (gate)
            {
                _unsharedTopics.Remove(topic);
                ClearFailureWhenFallbackEnds();
            }
        }

        public void RecordReadFailure(Exception failure)
        {
            lock (gate)
            {
                _isReadFailing = true;
                _lastFailure = failure.GetType().Name;
            }
        }

        public void RecordReadSuccess()
        {
            lock (gate)
            {
                _isReadFailing = false;
                ClearFailureWhenFallbackEnds();
            }
        }

        private bool IsFallbackActive() => _isReadFailing || _unsharedTopics.Count > 0;

        private void ClearFailureWhenFallbackEnds()
        {
            var at = now();
            foreach (var topic in _unsharedTopics.Where(topic => !hasLiveTripAt(topic, at)).ToList())
            {
                _unsharedTopics.Remove(topic);
                if (_announcedUnshared.Remove(topic))
                    _endedUnsharedToAnnounce.Add(topic);
            }

            if (!IsFallbackActive())
                _lastFailure = null;
        }
    }

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

        [LoggerMessage(LogLevel.Warning, "Reading tripped topics from the store ran into the store timeout; using this instance's own view")]
        public static partial void ReadTimedOut(ILogger logger);

        [LoggerMessage(LogLevel.Warning, "Writing trips to the store ran into the store timeout; the trips stay unshared on this instance")]
        public static partial void WriteTimedOut(ILogger logger);

        [LoggerMessage(LogLevel.Warning, "A store call is still running after {Seconds} seconds; no longer holding its in-flight guard")]
        public static partial void InFlightGuardReleased(ILogger logger, double seconds);
    }
}
