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
            s_logger.LogDebug("Outbox Sweeper Service is starting.");

            _timer = new Timer(Callback, null, TimeSpan.Zero, TimeSpan.FromSeconds(_options.TimerInterval));

            return Task.CompletedTask;
        }

        private async void Callback(object _)
        {
            await DoWorkAsync();
        }

        private async Task DoWorkAsync()
        {
            var lockId = await _distributedLock.ObtainLockAsync(LockingResourceName, CancellationToken.None);
            if (lockId != null)
            {
                s_logger.LogDebug("Outbox Sweeper looking for unsent messages");

                var scope = _serviceScopeFactory.CreateAsyncScope();
                try
                {
                    var outbox = scope.ServiceProvider.GetService<IAmAnOutbox<Message>>();
                    if (outbox == null)
                    {
                        s_logger.LogWarning("No outbox found in the service provider.");
                        return;
                    }

                    var outboxSync = outbox as IAmAnOutboxSync<Message>;
                    var outboxAsync = outbox as IAmAnOutboxAsync<Message>;

                    if (outboxSync == null && outboxAsync == null)
                    {
                        s_logger.LogWarning("Outbox does not implement IAmAnOutboxSync<Message> or IAmAnOutboxAsync<Message>.");
                        return;
                    }

                    IAmACommandProcessor commandProcessor = scope.ServiceProvider.GetService<IAmACommandProcessor>();

                    var outBoxSweeper = new OutboxSweeper(
                        millisecondsSinceSent: _options.MinimumMessageAge,
                        commandProcessor: commandProcessor,
                        _options.BatchSize,
                        _options.UseBulk,
                        _options.Args);

                    if (_options.UseBulk)
                    {
                        await SweepInBulk(outboxAsync, outBoxSweeper);
                    }
                    else
                    {
                        await SweepWithoutBulk(outboxAsync, outBoxSweeper);
                    }
                }
                catch (Exception e)
                {
                    s_logger.LogError(e, "Error while sweeping the outbox.");
                    throw;
                }
                finally
                {
                    await _distributedLock.ReleaseLockAsync(LockingResourceName, lockId, CancellationToken.None);
                    await scope.DisposeAsync();
                }
            }
            else
            {
                s_logger.LogWarning("Outbox Sweeper is still running - abandoning attempt.");
            }
            
            s_logger.LogDebug("Outbox Sweeper sleeping");
        }

        private static async Task SweepWithoutBulk(IAmAnOutboxAsync<Message> outboxAsync, OutboxSweeper outBoxSweeper)
        {
            if (outboxAsync != null)
            {
                await outBoxSweeper.SweepAsync();
            }
            else
            {
                outBoxSweeper.Sweep();
            }
        }

        private static async Task SweepInBulk(IAmAnOutboxAsync<Message> outboxAsync, OutboxSweeper outBoxSweeper)
        {
            if (outboxAsync != null)
            {
                await outBoxSweeper.SweepAsyncOutboxAsync();
            }
            else
            {
                outBoxSweeper.SweepAsyncOutbox();
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            s_logger.LogDebug("Outbox Sweeper Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
