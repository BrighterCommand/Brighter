#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Paramore.Brighter.Outbox.Hosting;

/// <summary>
/// A hosted service that measures, on a fixed interval, how many messages in the outbox have not been dispatched and
/// are older than the stale age, and writes each result to the <see cref="OutboxStaleMessageReadings"/>.
/// </summary>
public partial class TimedOutboxStaleMessageMonitor : IHostedService, IDisposable
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<TimedOutboxStaleMessageMonitor>();
    private static readonly MethodInfo s_countOverall =
        typeof(TimedOutboxStaleMessageMonitor).GetMethod(nameof(CountOverall), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly OutboxStaleMessageReadings _readings;
    private readonly TimedOutboxStaleMessageMonitorOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>
    /// Creates a monitor that is not yet running.
    /// </summary>
    /// <param name="serviceScopeFactory">The factory of the scope the monitor opens at start to resolve the outbox and the producers' configuration.</param>
    /// <param name="readings">The readings the monitor writes each measurement to.</param>
    /// <param name="options">The settings of the monitor.</param>
    /// <param name="timeProvider">The clock of the monitor; <see cref="TimeProvider.System"/> when null.</param>
    /// <exception cref="ConfigurationException">A setting in <paramref name="options"/> is outside the range the monitor accepts.</exception>
    public TimedOutboxStaleMessageMonitor(
        IServiceScopeFactory serviceScopeFactory,
        OutboxStaleMessageReadings readings,
        TimedOutboxStaleMessageMonitorOptions options,
        TimeProvider? timeProvider = null)
    {
        options.Validate();

        _serviceScopeFactory = serviceScopeFactory;
        _readings = readings;
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Starts the monitor.
    /// </summary>
    /// <param name="cancellationToken">Not used.</param>
    /// <remarks>
    /// Starts the <see cref="OutboxStaleMessageReadings"/> with the outbox's type name, and with topics when the outbox is an <see cref="IAmATopicCountingOutbox"/>
    /// and at least one publication has a topic that is not empty.
    /// Otherwise logs one Information entry saying that only the overall count is measured, and why. When the outbox cannot count by topic, that is the reason given.
    /// Then starts measuring in the background, without waiting: the first count runs one
    /// <see cref="TimedOutboxStaleMessageMonitorOptions.MeasurementInterval"/> after start, and the next ones on that interval.
    /// </remarks>
    /// <returns>A completed task.</returns>
    /// <exception cref="ConfigurationException">The container has no <see cref="IAmAnOutbox"/> or no <see cref="IAmProducersConfiguration"/>, which <c>AddProducers</c> registers; or the outbox is sync-only or implements neither outbox interface, so it does not implement
    /// <see cref="IAmAnOutboxAsync{T, TTransaction}"/> for the transaction type of the <see cref="IAmABoxTransactionProvider"/>; or the
    /// <see cref="Publication.StaleMessageAge"/> of a publication is zero or less, or greater than <see cref="TimedOutboxStaleMessageMonitorOptions.MaxStaleMessageAge"/>.</exception>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetService<IAmAnOutbox>();
        var producersConfiguration = scope.ServiceProvider.GetService<IAmProducersConfiguration>();
        var transactionProvider = scope.ServiceProvider.GetService<IAmABoxTransactionProvider>();

        if (outbox is null)
            throw new ConfigurationException($"The outbox stale-message monitor found no {nameof(IAmAnOutbox)} in the container; AddProducers registers it");
        if (producersConfiguration is null)
            throw new ConfigurationException($"The outbox stale-message monitor found no {nameof(IAmProducersConfiguration)} in the container; AddProducers registers it");
        var transactionType = ValidateOutboxCountsAsynchronously(outbox, transactionProvider);

        var publications = ReadPublications(producersConfiguration);
        ValidateStaleMessageAges(publications);
        var topicAges = BuildTopicAges(publications);

        var outboxType = outbox.GetType().Name;
        var countsByTopic = outbox is IAmATopicCountingOutbox;
        LogWhenTopicsAreNotMeasured(outboxType, countsByTopic, topicAges.Count);

        var measuresTopics = countsByTopic && topicAges.Count > 0;
        _readings.Start(outboxType, measuresTopics);
        var outBoxBag = ReadOutBoxBag(producersConfiguration);
        StartMeasuring(
            outboxType,
            BuildOverallCount(outbox, transactionType, outBoxBag),
            measuresTopics ? BuildPerTopicCount((IAmATopicCountingOutbox)outbox, topicAges, outBoxBag) : null);
        return Task.CompletedTask;
    }

    private static Dictionary<string, object> ReadOutBoxBag(IAmProducersConfiguration producersConfiguration) =>
        (producersConfiguration as ProducersConfiguration)?.OutBoxBag ?? new Dictionary<string, object>();

    private Func<CancellationToken, Task<int>> BuildOverallCount(IAmAnOutbox outbox, Type transactionType, Dictionary<string, object> outBoxBag) =>
        (Func<CancellationToken, Task<int>>)s_countOverall
            .MakeGenericMethod(transactionType)
            .Invoke(null, new object[] { outbox, _options.StaleMessageAge, _options.CountCeiling, outBoxBag })!;

    private static Func<CancellationToken, Task<int>> CountOverall<TTransaction>(
        IAmAnOutbox outbox, TimeSpan staleMessageAge, int countCeiling, Dictionary<string, object> outBoxBag)
    {
        var asyncOutbox = (IAmAnOutboxAsync<Message, TTransaction>)outbox;
        return cancellationToken => asyncOutbox.GetOutstandingMessageCountAsync(
            staleMessageAge, null, countCeiling, outBoxBag, cancellationToken);
    }

    private Func<CancellationToken, Task<IReadOnlyDictionary<RoutingKey, int>>> BuildPerTopicCount(
        IAmATopicCountingOutbox outbox, IReadOnlyDictionary<RoutingKey, TimeSpan> topicAges, Dictionary<string, object> outBoxBag) =>
        async cancellationToken => KeepTheRequestedTopics(
            await outbox.GetOutstandingMessageCountsByTopicAsync(
                topicAges, null, _options.CountCeiling, outBoxBag, cancellationToken),
            topicAges);

    private static IReadOnlyDictionary<RoutingKey, int> KeepTheRequestedTopics(
        IReadOnlyDictionary<RoutingKey, int> counts, IReadOnlyDictionary<RoutingKey, TimeSpan> topicAges) =>
        counts.Where(count => topicAges.ContainsKey(count.Key)).ToDictionary(count => count.Key, count => count.Value);

    private void StartMeasuring(
        string outboxType,
        Func<CancellationToken, Task<int>> countOverall,
        Func<CancellationToken, Task<IReadOnlyDictionary<RoutingKey, int>>>? countPerTopic)
    {
        var origin = _timeProvider.GetTimestamp();
        using (ExecutionContext.SuppressFlow())
            _ = Task.Run(() => MeasureUntilStoppedAsync(outboxType, origin, countOverall, countPerTopic));
    }

    private async Task MeasureUntilStoppedAsync(
        string outboxType,
        long origin,
        Func<CancellationToken, Task<int>> countOverall,
        Func<CancellationToken, Task<IReadOnlyDictionary<RoutingKey, int>>>? countPerTopic)
    {
        var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = _stopping.Token.Register(() => stopped.TrySetResult(true));
        using var alarm = new Alarm(_timeProvider);
        var abandoned = new AbandonedCalls();

        var measured = 0L;
        while (await WaitUntilAsync(DueTime(measured + 1), origin, stopped.Task, alarm.RingAfter))
        {
            abandoned.DisposeEnded();
            var latest = Elapsed(origin).Ticks / _options.MeasurementInterval.Ticks;
            SkipMissedDueTimes(outboxType, latest - measured - 1, countPerTopic is not null);
            measured = latest;
            await MeasureAsync(outboxType, alarm, abandoned, stopped.Task, countOverall, countPerTopic);
        }

        abandoned.DisposeEnded();
    }

    private async Task MeasureAsync(
        string outboxType,
        Alarm alarm,
        AbandonedCalls abandoned,
        Task stopped,
        Func<CancellationToken, Task<int>> countOverall,
        Func<CancellationToken, Task<IReadOnlyDictionary<RoutingKey, int>>>? countPerTopic)
    {
        var started = _timeProvider.GetTimestamp();
        var overall = new CountCall<int>(countOverall, _timeProvider);
        var perTopic = countPerTopic is null ? null : new CountCall<IReadOnlyDictionary<RoutingKey, int>>(countPerTopic, _timeProvider);
        var decided = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var deadline = WaitUntilAsync(_options.MeasurementTimeout, started, Task.WhenAny(decided.Task, stopped), alarm.RingAgainAfter);
        await Task.WhenAll(
            DecideAsync(outboxType, StaleMessageCount.Overall, overall, started, deadline, abandoned, CompleteOverall),
            perTopic is null ? Task.CompletedTask : DecideAsync(outboxType, StaleMessageCount.PerTopic, perTopic, started, deadline, abandoned, CompletePerTopic));
        decided.TrySetResult(true);
        await deadline;
    }

    private async Task DecideAsync<TCount>(
        string outboxType,
        StaleMessageCount count,
        CountCall<TCount> call,
        long started,
        Task<bool> deadline,
        AbandonedCalls abandoned,
        Action<string, TCount> complete)
    {
        await WhenAnyThenYieldAsync(call.Answer, deadline);
        if (!call.EndedBefore(started, _options.MeasurementTimeout) && await deadline)
        {
            Abandon(outboxType, count, call.Answer, call.Cancellation, abandoned);
            return;
        }

        using (call.Cancellation)
            complete(outboxType, await call.Answer);
    }

    private TimeSpan DueTime(long gridPoint) => TimeSpan.FromTicks(_options.MeasurementInterval.Ticks * gridPoint);

    private TimeSpan Elapsed(long origin) => _timeProvider.GetElapsedTime(origin);

    private void SkipMissedDueTimes(string outboxType, long missed, bool measuresTopics)
    {
        if (missed <= 0)
            return;

        var dueTimes = (int)Math.Min(missed, int.MaxValue);
        Skip(outboxType, StaleMessageCount.Overall, dueTimes);
        if (measuresTopics)
            Skip(outboxType, StaleMessageCount.PerTopic, dueTimes);
    }

    private void Skip(string outboxType, StaleMessageCount count, int dueTimes)
    {
        _readings.Skip(count, dueTimes);
        Log.DueTimesSkipped(s_logger, outboxType, count, dueTimes);
    }

    // Checks the clock again after the timer is armed, so a due time that passes while the timer is being created is not missed.
    private async Task<bool> WaitUntilAsync(TimeSpan due, long origin, Task stopped, Func<TimeSpan, Task> ringAfter)
    {
        while (!stopped.IsCompleted)
        {
            var wait = due - Elapsed(origin);
            if (wait <= TimeSpan.Zero)
                return true;

            var rang = ringAfter(wait);
            if (due - Elapsed(origin) <= TimeSpan.Zero)
                return true;

            await WhenAnyThenYieldAsync(rang, stopped);
        }

        return false;
    }

    private static async Task WhenAnyThenYieldAsync(Task first, Task second)
    {
#if NETSTANDARD2_0
        await Task.WhenAny(first, second);
        await Task.Yield();
#else
        await Task.WhenAny(first, second).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
#endif
    }

    private void CompleteOverall(string outboxType, int count)
    {
        var capped = Math.Min(count, _options.CountCeiling);
        _readings.CompleteOverall(capped);
        Log.OverallCountCompleted(s_logger, outboxType, capped);
    }

    private void CompletePerTopic(string outboxType, IReadOnlyDictionary<RoutingKey, int> counts)
    {
        var capped = CapAtTheCeiling(counts);
        _readings.CompletePerTopic(capped);
        Log.PerTopicCountCompleted(s_logger, outboxType, string.Join(", ", capped.Select(count => $"{count.Key}={count.Value}")));
    }

    private void Abandon(
        string outboxType, StaleMessageCount count, Task call, CancellationTokenSource cancellation, AbandonedCalls abandoned)
    {
        _readings.Abandon(count);
        Log.CountAbandoned(s_logger, outboxType, count, _options.MeasurementTimeout);
        abandoned.Add(call, cancellation);
        var cancelled = CancelOffTheLoopThread(cancellation);
        _ = Task.WhenAll(call, cancelled).ContinueWith(ended => _ = ended.Exception, TaskScheduler.Default);
    }

    private static Task CancelOffTheLoopThread(CancellationTokenSource cancellation)
    {
#if NETSTANDARD2_0
        return Task.Run(cancellation.Cancel);
#else
        return cancellation.CancelAsync();
#endif
    }

    private IReadOnlyDictionary<RoutingKey, int> CapAtTheCeiling(IReadOnlyDictionary<RoutingKey, int> counts) =>
        counts.ToDictionary(count => count.Key, count => Math.Min(count.Value, _options.CountCeiling));

    private IReadOnlyDictionary<RoutingKey, TimeSpan> BuildTopicAges(IEnumerable<Publication> publications) =>
        new ReadOnlyDictionary<RoutingKey, TimeSpan>(publications
            .Where(publication => !RoutingKey.IsNullOrEmpty(publication.Topic))
            .GroupBy(publication => publication.Topic!)
            .ToDictionary(
                topic => topic.Key,
                topic => topic.Min(publication => publication.StaleMessageAge ?? _options.StaleMessageAge)));

    private static void LogWhenTopicsAreNotMeasured(string outboxType, bool countsByTopic, int topicCount)
    {
        if (!countsByTopic)
            Log.OverallCountOnlyWithNoCapability(s_logger, outboxType);
        else if (topicCount == 0)
            Log.OverallCountOnlyWithNoMeasuredTopic(s_logger, outboxType);
    }

    private static Type ValidateOutboxCountsAsynchronously(IAmAnOutbox outbox, IAmABoxTransactionProvider? transactionProvider)
    {
        var transactionType = CommandProcessor.GetTransactionTypeFromTransactionProvider(transactionProvider);
        var asyncOutboxType = typeof(IAmAnOutboxAsync<,>).MakeGenericType(typeof(Message), transactionType);
        if (!asyncOutboxType.IsInstanceOfType(outbox))
            throw new ConfigurationException(
                $"The outbox stale-message monitor needs an outbox that counts asynchronously, but {outbox.GetType().Name} {DescribeMissingAsyncCount(outbox, transactionType)}");
        return transactionType;
    }

    private static string DescribeMissingAsyncCount(IAmAnOutbox outbox, Type transactionType) =>
        typeof(IAmAnOutboxSync<,>).MakeGenericType(typeof(Message), transactionType).IsInstanceOfType(outbox)
            ? "is sync-only"
            : $"implements neither {nameof(IAmAnOutboxSync<Message, object>)} nor {nameof(IAmAnOutboxAsync<Message, object>)}";

    private static IReadOnlyList<Publication> ReadPublications(IAmProducersConfiguration producersConfiguration) =>
        producersConfiguration.ProducerRegistry?.Producers.Select(producer => producer.Publication).ToList()
        ?? new List<Publication>();

    private static void ValidateStaleMessageAges(IEnumerable<Publication> publications)
    {
        var publication = publications.FirstOrDefault(HasInvalidStaleMessageAge);
        if (publication is not null)
            throw new ConfigurationException(
                $"The {nameof(Publication.StaleMessageAge)} of {DescribePublication(publication)} must be greater than zero and at most {TimedOutboxStaleMessageMonitorOptions.MaxStaleMessageAge}, but was {publication.StaleMessageAge}");
    }

    private static string DescribePublication(Publication publication) =>
        RoutingKey.IsNullOrEmpty(publication.Topic)
            ? "a publication with an empty topic"
            : $"the publication for topic {publication.Topic}";

    private static bool HasInvalidStaleMessageAge(Publication publication) =>
        publication.StaleMessageAge is { } staleMessageAge
        && (staleMessageAge <= TimeSpan.Zero || staleMessageAge > TimedOutboxStaleMessageMonitorOptions.MaxStaleMessageAge);

    /// <summary>
    /// Stops the monitor.
    /// </summary>
    /// <param name="cancellationToken">The host's stop token.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Signals the monitor to stop and returns without waiting.
    /// </summary>
    public void Dispose()
    {
        _stopping.Cancel();
    }

    private sealed class CountCall<TCount>
    {
        private readonly TimeProvider _timeProvider;
        private long _ended;

        public CountCall(Func<CancellationToken, Task<TCount>> count, TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
            Answer = Task.Run(() => CallAsync(count));
        }

        public CancellationTokenSource Cancellation { get; } = new();

        public Task<TCount> Answer { get; }

        public bool EndedBefore(long started, TimeSpan timeout) =>
            Answer.IsCompleted && _timeProvider.GetElapsedTime(started, _ended) < timeout;

        // Awaits the outbox's task with no forced yield, so the end is recorded as soon as the outbox's task completes.
        private async Task<TCount> CallAsync(Func<CancellationToken, Task<TCount>> count)
        {
            try
            {
                return await count(Cancellation.Token);
            }
            finally
            {
                _ended = _timeProvider.GetTimestamp();
            }
        }
    }

    // Holds the token source of each abandoned call until the loop sees that the call has ended, so only the loop disposes it.
    private sealed class AbandonedCalls
    {
        private readonly object _lock = new();
        private readonly List<(Task Call, CancellationTokenSource Cancellation)> _calls = new();

        public void Add(Task call, CancellationTokenSource cancellation)
        {
            lock (_lock)
                _calls.Add((call, cancellation));
        }

        public void DisposeEnded()
        {
            lock (_lock)
                _calls.RemoveAll(DisposeWhenEnded);
        }

        private static bool DisposeWhenEnded((Task Call, CancellationTokenSource Cancellation) abandoned)
        {
            if (!abandoned.Call.IsCompleted)
                return false;

            abandoned.Cancellation.Dispose();
            return true;
        }
    }

    // Keeps the timer of the latest due time, so the deadline of that due time's measurement re-arms it instead of creating another.
    private sealed class Alarm : IDisposable
    {
        private readonly TimeProvider _timeProvider;
        private ITimer? _timer;
        private Bell _bell = new();

        public Alarm(TimeProvider timeProvider) => _timeProvider = timeProvider;

        public Task RingAfter(TimeSpan wait)
        {
            _timer?.Dispose();
            var bell = new Bell();
            _bell = bell;
            var rang = bell.Arm();
            _timer = _timeProvider.CreateTimer(_ => bell.Ring(), null, wait, Timeout.InfiniteTimeSpan);
            return rang;
        }

        public Task RingAgainAfter(TimeSpan wait)
        {
            if (_timer is null)
                return RingAfter(wait);

            var rang = _bell.Arm();
            _timer.Change(wait, Timeout.InfiniteTimeSpan);
            return rang;
        }

        public void Dispose() => _timer?.Dispose();
    }

    private sealed class Bell
    {
        private TaskCompletionSource<bool> _rang = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Arm()
        {
            var rang = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref _rang, rang);
            return rang.Task;
        }

        public void Ring() => Volatile.Read(ref _rang).TrySetResult(true);
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Debug, "Outbox stale-message monitor counted {StaleCount} stale messages overall for {OutboxType}")]
        public static partial void OverallCountCompleted(ILogger logger, string outboxType, int staleCount);

        [LoggerMessage(LogLevel.Debug, "Outbox stale-message monitor counted stale messages per topic for {OutboxType}: {StaleCounts}")]
        public static partial void PerTopicCountCompleted(ILogger logger, string outboxType, string staleCounts);

        [LoggerMessage(LogLevel.Warning, "Outbox stale-message monitor skipped {DueTimes} due times of the {Count} count for {OutboxType}")]
        public static partial void DueTimesSkipped(ILogger logger, string outboxType, StaleMessageCount count, int dueTimes);

        [LoggerMessage(LogLevel.Warning, "Outbox stale-message monitor abandoned the {Count} count for {OutboxType} after {Timeout}")]
        public static partial void CountAbandoned(ILogger logger, string outboxType, StaleMessageCount count, TimeSpan timeout);

        [LoggerMessage(LogLevel.Information, "Outbox stale-message monitor measures only the overall count for {OutboxType}: no capability to count by topic")]
        public static partial void OverallCountOnlyWithNoCapability(ILogger logger, string outboxType);

        [LoggerMessage(LogLevel.Information, "Outbox stale-message monitor measures only the overall count for {OutboxType}: no measured topic")]
        public static partial void OverallCountOnlyWithNoMeasuredTopic(ILogger logger, string outboxType);
    }
}
