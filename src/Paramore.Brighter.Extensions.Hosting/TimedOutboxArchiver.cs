using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Extensions.Hosting
{

    public class TimedOutboxArchiver<TMessage, TTransaction> : IHostedService, IDisposable where TMessage : Message
    {
        private readonly TimedOutboxArchiverOptions _options;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<TimedOutboxSweeper>();
        private readonly IAmAnOutbox<TMessage, TTransaction> _outbox;
        private readonly IAmAnArchiveProvider _archiveProvider;
        private Timer _timer;

        public TimedOutboxArchiver(
            IAmAnOutbox<TMessage, TTransaction> outbox, 
            IAmAnArchiveProvider archiveProvider,
            TimedOutboxArchiverOptions options)
        {
            _outbox = outbox;
            _archiveProvider = archiveProvider;
            _options = options;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            s_logger.LogInformation("Outbox Archiver Service is starting.");

            _timer = new Timer(Archive, null, TimeSpan.Zero, TimeSpan.FromSeconds(_options.TimerInterval));

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

        private void Archive(object state)
        {
            s_logger.LogInformation("Outbox Archiver looking for messages to Archive");

            try
            {
                var outBoxArchiver = new OutboxArchiver<TMessage, TTransaction>(
                    _outbox,
                    _archiveProvider,
                    _options.BatchSize);

                outBoxArchiver.Archive(_options.MinimumAge);
            }
            catch (Exception e)
            {
                s_logger.LogError(e, "Error while sweeping the outbox.");
            }

            s_logger.LogInformation("Outbox Sweeper sleeping");
        }
    }
}
