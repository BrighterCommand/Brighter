using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Extensions.Hosting
{

    public class TimedOutboxArchiver : IHostedService, IDisposable
    {
        private readonly TimedOutboxArchiverOptions _options;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<TimedOutboxSweeper>();
        private IAmAnOutboxSync<Message> _outboxSync;
        private IAmAnOutboxAsync<Message> _outboxAsync;
        private IAmAnArchiveProvider _archiveProvider;
        private readonly IDistributedLock _distributedLock;
        private Timer _timer;

        private const string LockingResourceName = "Archiver";
        
        public TimedOutboxArchiver(IAmAnOutbox<Message> outbox, IAmAnArchiveProvider archiveProvider,
            IDistributedLock distributedLock, TimedOutboxArchiverOptions options)
        {
            if (outbox is IAmAnOutboxSync<Message> syncBox)
                _outboxSync = syncBox;
            
            if (outbox is IAmAnOutboxAsync<Message> asyncBox)
                _outboxAsync = asyncBox;
            _archiveProvider = archiveProvider;
            _distributedLock = distributedLock;
            _options = options;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            s_logger.LogDebug("Outbox Archiver Service is starting.");

            _timer = new Timer(async (e) => await Archive(e, cancellationToken), null, TimeSpan.Zero,
                TimeSpan.FromSeconds(_options.TimerInterval));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            s_logger.LogDebug("Outbox Archiver Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer.Dispose();
        }

        private async Task Archive(object state, CancellationToken cancellationToken)
        {
            if (_outboxSync == null && _outboxAsync == null)
            {
                s_logger.LogWarning("No outbox implementation found. Cannot archive messages.");
                return;
            }

            var lockId = await _distributedLock.ObtainLockAsync(LockingResourceName, cancellationToken); 
            if (lockId != null)
            {
                s_logger.LogDebug("Outbox Archiver looking for messages to Archive");
                try
                {
                    var outBoxArchiver = new OutboxArchiver(
                        _outboxAsync as IAmAnOutbox<Message> ?? _outboxSync,
                        _archiveProvider,
                        _options.BatchSize);

                    if (_outboxAsync != null)
                    {
                        await outBoxArchiver.ArchiveAsync(_options.MinimumAge, cancellationToken, _options.ParallelArchiving);
                    }
                    else
                    {
                        outBoxArchiver.Archive(_options.MinimumAge);
                    }
                }
                catch (Exception e)
                {
                    s_logger.LogError(e, "Error while sweeping the outbox.");
                }
                finally
                {
                    await _distributedLock.ReleaseLockAsync(LockingResourceName, lockId, cancellationToken);
                }

                s_logger.LogDebug("Outbox Sweeper sleeping");
            }
            else
            {
                s_logger.LogDebug("Outbox Archiver is still running - abandoning attempt.");
            }
            
        }
    }
}
