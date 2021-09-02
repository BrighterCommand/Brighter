using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Paramore.Brighter.Extensions.Hosting
{
    public class TimedOutboxSweeper : IHostedService, IDisposable
    {
        private readonly IAmAnOutboxViewer<Message> _outbox;
        private readonly IAmACommandProcessor _commandProcessor;
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<TimedOutboxSweeper>();
        private Timer _timer;

        public TimedOutboxSweeper (IAmAnOutboxViewer<Message> outbox, IAmACommandProcessor commandProcessor)
        {
            _outbox = outbox;
            _commandProcessor = commandProcessor;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            s_logger.LogInformation("Outbox Sweeper Service is starting.");

            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

            return Task.CompletedTask;
        }

        private void DoWork(object state)
        {
            s_logger.LogInformation("Outbox Sweeper looking for unsent messages");

            var outBoxSweeper = new OutboxSweeper(
                milliSecondsSinceSent:5000, 
                outbox:_outbox, 
                commandProcessor:_commandProcessor);

            outBoxSweeper.Sweep();
            
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
