#region Licence

/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Paramore.Brighter.Outbox.Hosting
{
    /// <summary>
    /// Runs a sweeper that will find outstanding messages in the Outbox and produce them via a broker
    /// Uses a time to run at pre-defined intervals
    /// </summary>
    /// <remarks>
    /// Sweeps run on a dedicated thread, not on the thread pool. A thread-pool timer cannot fire while every
    /// pool worker is blocked, and that is exactly when unsent messages pile up in the Outbox (#4560).
    /// </remarks>
    public partial class TimedOutboxSweeper : IHostedService, IDisposable
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IDistributedLock _distributedLock;
        private readonly TimedOutboxSweeperOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly IAmABrighterSweeperMeter _meter;
        private readonly TimeSpan _interval;
        private readonly CancellationTokenSource _stopping = new();
        private readonly AutoResetEvent _due = new(false);
        private readonly TaskCompletionSource<bool> _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<TimedOutboxSweeper>();
        private Thread? _sweepThread;
        private bool _disposed;
        private const string LockingResourceName = "OutboxSweeper";

        /// <summary>
        /// Creates an instance of a Timed Outbox Sweeper
        /// </summary>
        /// <param name="serviceScopeFactory">Needed to create a scope within which to create a <see cref="CommandProcessor"/></param>
        /// <param name="distributedLock">Used to ensure that only one instance of the <see cref="TimedOutboxSweeper"/> is running</param>
        /// <param name="options">The <see cref="TimedOutboxSweeperOptions"/> that can be used to configure how this runs, such as interval or age</param>
        /// <param name="timeProvider">The clock that schedules sweeps; defaults to <see cref="TimeProvider.System"/></param>
        /// <param name="meter">Records the sweeper's health (see ADR 0081); defaults to <see cref="NullSweeperMeter"/>, which records nothing</param>
        /// <exception cref="ConfigurationException">Thrown when <see cref="TimedOutboxSweeperOptions.TimerInterval"/> is less than one second</exception>
        public TimedOutboxSweeper(
            IServiceScopeFactory serviceScopeFactory,
            IDistributedLock distributedLock,
            TimedOutboxSweeperOptions options,
            TimeProvider? timeProvider = null,
            IAmABrighterSweeperMeter? meter = null
        )
        {
            if (options.TimerInterval < 1)
                throw new ConfigurationException(
                    $"{nameof(TimedOutboxSweeperOptions)}.{nameof(TimedOutboxSweeperOptions.TimerInterval)} must be at least 1 second, but was {options.TimerInterval}");

            _serviceScopeFactory = serviceScopeFactory;
            _distributedLock = distributedLock;
            _options = options;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _meter = meter ?? NullSweeperMeter.Instance;
            _interval = TimeSpan.FromSeconds(options.TimerInterval);
        }

        /// <summary>
        /// Starts an instance of the <see cref="TimedOutboxSweeper"/> at the configured interval. See <see cref="TimedOutboxSweeperOptions.TimerInterval"/>
        /// </summary>
        /// <param name="cancellationToken">Not used</param>
        /// <returns>A completed task to allow other background services to be run</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            Log.OutboxSweeperServiceIsStarting(s_logger);

            _sweepThread = new Thread(SweepUntilStopped) { IsBackground = true, Name = "Brighter Outbox Sweeper" };
            // Start with an empty context, so sweeps do not carry the starting caller's ambient state (AsyncLocals)
            using (ExecutionContext.SuppressFlow())
                _sweepThread.Start();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops the <see cref="TimedOutboxSweeper"/>, waiting for any sweep in flight to finish
        /// </summary>
        /// <param name="cancellationToken">Abandons the wait for a sweep in flight</param>
        /// <returns>A task that completes when the sweeper has stopped</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            Log.OutboxSweeperServiceIsStopping(s_logger);

            if (_sweepThread is null)
                return Task.CompletedTask;

            _stopping.Cancel();
            return WaitForStop(cancellationToken);
        }

        /// <summary>
        /// Stops the sweeper's thread and releases the resources it uses
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _stopping.Cancel();

            // A sweep still in flight goes on using these; let it finish rather than pull them away.
            if (_sweepThread is not null && !_sweepThread.Join(TimeSpan.Zero))
                return;

            _stopping.Dispose();
            _due.Dispose();
        }

        private async Task WaitForStop(CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
            {
                await _stopped.Task;
                return;
            }

            var abandoned = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => abandoned.TrySetResult(true)))
                await Task.WhenAny(_stopped.Task, abandoned.Task);
        }

        private void SweepUntilStopped()
        {
            try
            {
                var due = _timeProvider.GetUtcNow();
                while (WaitUntil(due))
                {
                    var started = _timeProvider.GetUtcNow();
                    Record(() => _meter.RecordTickLag(started - due));

                    Sweep();

                    due = NextDue(due, started);
                }
            }
            finally
            {
                _stopped.TrySetResult(true);
            }
        }

        // Waits on this thread, never on the pool: the real-time timeout wakes the sweeper even when no pool
        // thread is free to run the TimeProvider's timer callback. The callback lets a test clock wake it too.
        private bool WaitUntil(DateTimeOffset due)
        {
            while (!_stopping.IsCancellationRequested)
            {
                var wait = due - _timeProvider.GetUtcNow();
                if (wait <= TimeSpan.Zero)
                    return true;

                using var timer = _timeProvider.CreateTimer(_ => SignalDue(), null, wait, Timeout.InfiniteTimeSpan);
                // The clock may have moved on before the timer existed to see it, so look again before waiting.
                if (due - _timeProvider.GetUtcNow() <= TimeSpan.Zero)
                    return true;

                WaitHandle.WaitAny(new[] { _due, _stopping.Token.WaitHandle }, wait);
            }

            return false;
        }

        private void SignalDue()
        {
            try { _due.Set(); }
            catch (ObjectDisposedException) { }
        }

        // Keeps to the schedule, so a late or overrunning sweep shows as lag on the next one. A sweep that started
        // a whole interval or more late re-anchors the schedule, so the sweeper never sweeps back to back to catch up.
        private DateTimeOffset NextDue(DateTimeOffset due, DateTimeOffset started)
        {
            var anchor = started - due >= _interval ? started : due;
            return anchor + _interval;
        }

        private void Sweep()
        {
            var started = _timeProvider.GetTimestamp();
            var outcome = SweepOnce();
            var duration = _timeProvider.GetElapsedTime(started);
            Record(() => _meter.RecordSweep(outcome, duration));

            Log.OutboxSweeperSleeping(s_logger);
        }

        private SweepOutcome SweepOnce()
        {
            try
            {
                // Runs the sweep in a context on the sweeper's own thread, so its awaits resume here rather than on the pool:
                // a producer such as Kafka completes a send from its own thread with asynchronous continuations.
                return BrighterAsyncContext.Run(SweepAsync);
            }
            catch (Exception e)
            {
                Log.OutboxSweepFailed(s_logger, e);
                return SweepOutcome.Failed;
            }
        }

        private async Task<SweepOutcome> SweepAsync()
        {
            var lockId = await _distributedLock.ObtainLockAsync(LockingResourceName, CancellationToken.None);
            if (lockId == null)
            {
                Log.OutboxSweeperIsStillRunningAbandoningAttempt(s_logger);
                return SweepOutcome.LockUnavailable;
            }

            try
            {
                Log.OutboxSweeperLookingForUnsentMessages(s_logger);

                using var scope = _serviceScopeFactory.CreateScope();
                IAmAnOutboxProducerMediator outboxProducerMediator = scope.ServiceProvider.GetRequiredService<IAmAnOutboxProducerMediator>();

                var outBoxSweeper = new OutboxSweeper(
                    timeSinceSent: _options.MinimumMessageAge,
                    outboxProducerMediator: outboxProducerMediator,
                    new InMemoryRequestContextFactory(),
                    _options.BatchSize,
                    _options.UseBulk,
                    _options.Args);

                await outBoxSweeper.SweepAsync();
                return SweepOutcome.Completed;
            }
            finally
            {
                await _distributedLock.ReleaseLockAsync(LockingResourceName, lockId, CancellationToken.None);
            }
        }

        private static void Record(Action record)
        {
            try { record(); }
            catch (Exception e) { Log.SweeperMeterFailed(s_logger, e); }
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Information, "Outbox Sweeper Service is starting.")]
            public static partial void OutboxSweeperServiceIsStarting(ILogger logger);

            [LoggerMessage(LogLevel.Information, "Outbox Sweeper Service is stopping.")]
            public static partial void OutboxSweeperServiceIsStopping(ILogger logger);
            
            [LoggerMessage(LogLevel.Information, "Outbox Sweeper looking for unsent messages")]
            public static partial void OutboxSweeperLookingForUnsentMessages(ILogger logger);
            
            [LoggerMessage(LogLevel.Warning, "Outbox Sweeper is still running - abandoning attempt.")]
            public static partial void OutboxSweeperIsStillRunningAbandoningAttempt(ILogger logger);
            
            [LoggerMessage(LogLevel.Information, "Outbox Sweeper sleeping")]
            public static partial void OutboxSweeperSleeping(ILogger logger);

            [LoggerMessage(LogLevel.Error, "Outbox Sweeper failed to sweep the outbox; it will try again on the next sweep")]
            public static partial void OutboxSweepFailed(ILogger logger, Exception exception);

            [LoggerMessage(LogLevel.Warning, "Outbox Sweeper could not record a metric")]
            public static partial void SweeperMeterFailed(ILogger logger, Exception exception);
        }
    }
}
