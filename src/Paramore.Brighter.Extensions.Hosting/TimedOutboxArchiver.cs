using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Extensions.Hosting
{

    public class TimedOutboxArchiver(
        IServiceScopeFactory serviceScopeFactory,
        IDistributedLock distributedLock,
        TimedOutboxArchiverOptions options)
        : IHostedService, IDisposable
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<TimedOutboxSweeper>();
        private Timer _timer;

        private const string LockingResourceName = "Archiver";

        public Task StartAsync(CancellationToken cancellationToken)
        {
            s_logger.LogInformation("Outbox Archiver Service is starting");

            _timer = new Timer(async (e) => await Archive(e, cancellationToken), null, TimeSpan.Zero,
                TimeSpan.FromSeconds(options.TimerInterval));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            s_logger.LogInformation("Outbox Archiver Service is stopping");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer.Dispose();
        }

        private async Task Archive(object state, CancellationToken cancellationToken)
        {
            if (await distributedLock.ObtainLockAsync(LockingResourceName, cancellationToken))
            {
                var scope = serviceScopeFactory.CreateScope();
                s_logger.LogInformation("Outbox Archiver looking for messages to Archive");
                try
                {
                    IAmAnExternalBusService externalBusService = scope.ServiceProvider.GetService<IAmAnExternalBusService>();
                    
                    await externalBusService.ArchiveAsync(options.MinimumAge, cancellationToken);
                }
                catch (Exception e)
                {
                    s_logger.LogError(e, "Error while sweeping the outbox");
                }
                finally
                {
                    await distributedLock.ReleaseLockAsync(LockingResourceName, cancellationToken);
                }

                s_logger.LogInformation("Outbox Sweeper sleeping");
            }
            else
            {
                s_logger.LogWarning("Outbox Archiver is still running - abandoning attempt");
            }
            
        }
    }
}
