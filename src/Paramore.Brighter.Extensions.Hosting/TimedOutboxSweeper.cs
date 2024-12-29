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
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Paramore.Brighter.Extensions.Hosting
{
    /// <summary>
    /// Runs a sweeper that will find outstanding messages in the Outbox and produce them via a broker
    /// Uses a time to run at pre-defined intervals
    /// </summary>
    public class TimedOutboxSweeper : IHostedService, IDisposable
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IDistributedLock _distributedLock;
        private readonly TimedOutboxSweeperOptions _options;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<TimedOutboxSweeper>();
        private Timer _timer;
        private const string LockingResourceName = "OutboxSweeper";

        /// <summary>
        /// Creates an instance of a Timed Outbox Sweeper
        /// </summary>
        /// <param name="serviceScopeFactory">Needed to create a scope within which to create a <see cref="CommandProcessor"/></param>
        /// <param name="distributedLock">Used to ensure that only one instance of the <see cref="TimedOutboxSweeper"/> is running</param>
        /// <param name="options">The <see cref="TimedOutboxSweeperOptions"/> that can be used to configure how this runs, such as interval or age</param>
        public TimedOutboxSweeper(
            IServiceScopeFactory serviceScopeFactory,
            IDistributedLock distributedLock,
            TimedOutboxSweeperOptions options
        )
        {
            _serviceScopeFactory = serviceScopeFactory;
            _distributedLock = distributedLock;
            _options = options;
        }

        /// <summary>
        /// Starts an instance of the <see cref="TimedOutboxSweeper"/> at the configured interval. See <see cref="TimedOutboxSweeperOptions.TimerInterval"/>
        /// </summary>
        /// <param name="cancellationToken">Cancels execution of the <see cref="TimedOutboxSweeper"/></param>
        /// <returns>A completed task to allow other background services to be run</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            s_logger.LogInformation("Outbox Sweeper Service is starting.");

            _timer = new Timer((e) => Sweep(e), null, TimeSpan.Zero,
                TimeSpan.FromSeconds(_options.TimerInterval));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops the <see cref="TimedOutboxSweeper"/>
        /// </summary>
        /// <param name="cancellationToken">Not used</param>
        /// <returns>A completed task to allow other background services to be stopped</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            s_logger.LogInformation("Outbox Sweeper Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Cleans up the <see cref="Timer"/> used by the <see cref="TimedOutboxSweeper"/>
        /// </summary>
        public void Dispose()
        {
            _timer?.Dispose();
        }

        private void Sweep(object state)
        {
            var lockId = _distributedLock.ObtainLockAsync(LockingResourceName, CancellationToken.None).Result;
            if (lockId != null)
            {
                s_logger.LogInformation("Outbox Sweeper looking for unsent messages");

                var scope = _serviceScopeFactory.CreateScope();
                try
                {
                    IAmACommandProcessor commandProcessor = scope.ServiceProvider.GetService<IAmACommandProcessor>();

                    var outBoxSweeper = new OutboxSweeper(
                        timeSinceSent: _options.MinimumMessageAge,
                        commandProcessor: commandProcessor,
                        new InMemoryRequestContextFactory(),
                        _options.BatchSize,
                        _options.UseBulk,
                        _options.Args);

                    if (_options.UseBulk)
                        outBoxSweeper.SweepAsyncOutbox();
                    else
                        outBoxSweeper.Sweep();
                }
                finally
                {
                    //on a timer thread, so blocking is OK
                    _distributedLock.ReleaseLockAsync(LockingResourceName, lockId, CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                    scope.Dispose();
                }
            }
            else
            {
                s_logger.LogWarning("Outbox Sweeper is still running - abandoning attempt.");
            }

            s_logger.LogInformation("Outbox Sweeper sleeping");
        }
    }
}
