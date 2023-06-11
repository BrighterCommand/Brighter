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
        private IAmAnOutbox<Message> _outbox;
        private IAmAnArchiveProvider _archiveProvider;
        private Timer _timer;

        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public TimedOutboxArchiver(IAmAnOutbox<Message> outbox, IAmAnArchiveProvider archiveProvider,
            TimedOutboxArchiverOptions options)
        {
            _outbox = outbox;
            _archiveProvider = archiveProvider;
            _options = options;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            s_logger.LogInformation("Outbox Archiver Service is starting.");

            _timer = new Timer(async (e) => await Archive(e, cancellationToken), null, TimeSpan.Zero, TimeSpan.FromSeconds(_options.TimerInterval));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            s_logger.LogInformation("Outbox Archiver Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer.Dispose();
        }

        private async Task Archive(object state, CancellationToken cancellationToken)
        {
            if (await _semaphore.WaitAsync(TimeSpan.Zero, cancellationToken))
            {
                s_logger.LogInformation("Outbox Archiver looking for messages to Archive");
                try
                {
                    var outBoxArchiver = new OutboxArchiver(
                        _outbox,
                        _archiveProvider,
                        _options.BatchSize);

                    await outBoxArchiver.ArchiveAsync(_options.MinimumAge, cancellationToken, _options.ParallelArchiving);
                }
                catch (Exception e)
                {
                    s_logger.LogError(e, "Error while sweeping the outbox.");
                }
                finally
                {
                    _semaphore.Release();
                }

                s_logger.LogInformation("Outbox Sweeper sleeping");
            }
            else
            {
                s_logger.LogWarning("Outbox Archiver is still running - abandoning attempt.");
            }
            
        }
    }
}
