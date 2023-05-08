using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Timer = System.Timers.Timer;

namespace Paramore.Brighter.Extensions.Hosting
{
    public class TimedOutboxSweeper : IHostedService, IDisposable
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly TimedOutboxSweeperOptions _options;
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<TimedOutboxSweeper>();

        private Timer _timer;
        //private Timer _timer;
        
        public TimedOutboxSweeper (IServiceScopeFactory serviceScopeFactory, TimedOutboxSweeperOptions options)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _options = options;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            s_logger.LogInformation("Outbox Sweeper Service is starting.");

            //_timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(_options.TimerInterval));
            var milliseconds = _options.TimerInterval * 1000;
            _timer = new Timer(milliseconds);
            _timer.Elapsed += new ElapsedEventHandler(OnElapsed);
            _timer.Enabled = true;
            _timer.AutoReset = false;

            return Task.CompletedTask;
        }

        private void OnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
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
                    outBoxSweeper.SweepAsyncOutbox();
                else
                    outBoxSweeper.Sweep();
            }
            catch (Exception e)
            {
                s_logger.LogError(e, "Error while sweeping the outbox.");
                throw;
            }
            finally
            {
                scope.Dispose();
                ((Timer)sender).Enabled = true;
            }
            
            s_logger.LogInformation("Outbox Sweeper sleeping");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            s_logger.LogInformation("Outbox Sweeper Service is stopping.");

            //_timer?.Change(Timeout.Infinite, 0);
            _timer.Stop();

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
