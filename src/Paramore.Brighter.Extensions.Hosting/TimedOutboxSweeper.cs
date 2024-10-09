﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Paramore.Brighter.Extensions.Hosting
{
    public class TimedOutboxSweeper : IHostedService, IDisposable
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IDistributedLock _distributedLock;
        private readonly TimedOutboxSweeperOptions _options;
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<TimedOutboxSweeper>();
        private Timer _timer;
        private const string LockingResourceName = "OutboxSweeper";

        public TimedOutboxSweeper(IServiceScopeFactory serviceScopeFactory, IDistributedLock distributedLock,
            TimedOutboxSweeperOptions options)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _distributedLock = distributedLock;
            _options = options;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            s_logger.LogInformation("Outbox Sweeper Service is starting.");

            _timer = new Timer(Callback, null, TimeSpan.Zero, TimeSpan.FromSeconds(_options.TimerInterval));

            return Task.CompletedTask;
        }

        private async void Callback(object _)
        {
            await DoWorkAsync();
        }

        private async Task DoWorkAsync()
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
                        millisecondsSinceSent: _options.MinimumMessageAge,
                        commandProcessor: commandProcessor,
                        _options.BatchSize,
                        _options.UseBulk,
                        _options.Args);

                    if (_options.UseBulk)
                        await outBoxSweeper.SweepAsyncOutboxAsync();
                    else
                        await outBoxSweeper.SweepAsync();
                }
                catch (Exception e)
                {
                    s_logger.LogError(e, "Error while sweeping the outbox.");
                    throw;
                }
                finally
                {
                    _distributedLock.ReleaseLockAsync(LockingResourceName, lockId, CancellationToken.None).Wait();
                    scope.Dispose();
                }
            }
            else
            {
                s_logger.LogInformation("Outbox Sweeper is still running - abandoning attempt.");
            }
            
            s_logger.LogInformation("Outbox Sweeper sleeping");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            s_logger.LogInformation("Outbox Sweeper Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
