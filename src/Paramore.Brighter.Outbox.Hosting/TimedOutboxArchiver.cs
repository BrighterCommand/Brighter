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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
// ReSharper disable StaticMemberInGenericType

namespace Paramore.Brighter.Outbox.Hosting
{
    /// <summary>
    /// The archiver will find messages in the outbox that are older than a certain age and archive them
    /// </summary>
    public partial class TimedOutboxArchiver<TMessage, TTransaction> : IHostedService, IDisposable where TMessage : Message
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<TimedOutboxSweeper>();
        private Timer? _timer;
        private readonly OutboxArchiver<TMessage, TTransaction> _archiver;
        private readonly IDistributedLock _distributedLock;
        private readonly TimedOutboxArchiverOptions _options;

        /// <summary>
        /// The archiver will find messages in the outbox that are older than a certain age and archive them
        /// </summary>
        /// <param name="archiver">The archiver to use</param>
        /// <param name="distributedLock">Used to ensure that only one instance of the <see cref="TimedOutboxSweeper"/> is running</param>
        /// <param name="options">The <see cref="TimedOutboxArchiverOptions"/> that control how the archiver runs, such as interval</param>
        public TimedOutboxArchiver(
            OutboxArchiver<TMessage, TTransaction> archiver,
            IDistributedLock distributedLock,
            TimedOutboxArchiverOptions options)
        {
            _archiver = archiver;
            _distributedLock = distributedLock;
            _options = options;
        }

        private const string LockingResourceName = "Archiver";

        /// <summary>
        /// Starts the archiver, which will run at an interval to find messages in the outbox that are older than a certain age and archive them
        /// </summary>
        /// <param name="cancellationToken">Will stop the archiver if signalled</param>
        /// <returns>A completed task to allow other background services to run</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            Log.OutboxArchiverServiceIsStarting(s_logger);

            _timer = new Timer(_ => Archive(cancellationToken).GetAwaiter().GetResult(), 
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(_options.TimerInterval));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops the archiver from running
        /// </summary>
        /// <param name="cancellationToken">Not used</param>
        /// <returns>A completed task to allow other background services to run</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            Log.OutboxArchiverServiceIsStopping(s_logger);

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Disposes of the timer resource used by the archiver
        /// </summary>
        public void Dispose()
        {
            _timer?.Dispose();
        }

        private async Task Archive(CancellationToken cancellationToken)
        {
            try
            {
                var lockId = await _distributedLock.ObtainLockAsync(LockingResourceName, cancellationToken);
                if (lockId == null)
                {
                    Log.OutboxArchiverIsStillRunningAbandoningAttempt(s_logger);
                    return;
                }

                Log.OutboxArchiverLookingForMessagesToArchive(s_logger);
                try
                {
                    await _archiver.ArchiveAsync(_options.MinimumAge, new RequestContext(), cancellationToken);
                }
                finally
                {
                    await _distributedLock.ReleaseLockAsync(LockingResourceName, lockId, cancellationToken);
                }

                Log.OutboxSweeperSleeping(s_logger);
            }
            catch (Exception e)
            {
                Log.ErrorWhileSweepingTheOutbox(s_logger, e);
            }
            finally
            {
                Log.OutboxSweeperSleeping(s_logger);
            }
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Information, "Outbox Archiver Service is starting")]
            public static partial void OutboxArchiverServiceIsStarting(ILogger logger);

            [LoggerMessage(LogLevel.Information, "Outbox Archiver Service is stopping")]
            public static partial void OutboxArchiverServiceIsStopping(ILogger logger);
            
            [LoggerMessage(LogLevel.Information, "Outbox Archiver looking for messages to Archive")]
            public static partial void OutboxArchiverLookingForMessagesToArchive(ILogger logger);

            [LoggerMessage(LogLevel.Error, "Error while sweeping the outbox")]
            public static partial void ErrorWhileSweepingTheOutbox(ILogger logger, Exception exception);

            [LoggerMessage(LogLevel.Information, "Outbox Sweeper sleeping")]
            public static partial void OutboxSweeperSleeping(ILogger logger);

            [LoggerMessage(LogLevel.Warning, "Outbox Archiver is still running - abandoning attempt")]
            public static partial void OutboxArchiverIsStillRunningAbandoningAttempt(ILogger logger);
        }
    }
}

