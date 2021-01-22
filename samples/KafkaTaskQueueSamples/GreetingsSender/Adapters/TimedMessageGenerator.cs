using System;
using System.Threading;
using System.Threading.Tasks;
using Greetings.Ports.Commands;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;

namespace GreetingsSender.Adapters
{
    public class TimedMessageGenerator : IHostedService, IDisposable
    {
        private readonly IAmACommandProcessor _processor;
        private readonly ILogger<TimedMessageGenerator> _logger;
        private Timer _timer;
        private long _iteration = 0;

        public TimedMessageGenerator(IAmACommandProcessor processor, ILogger<TimedMessageGenerator> logger)
        {
            _processor = processor;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Kafka Message Generator is starting.");

            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Kafka Message Generator is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }
        
        private void DoWork(object state)
        {
            var messageId = Guid.NewGuid();
            _iteration++;

            _processor.Post(new GreetingEvent{ Id = messageId, Greeting = $"Hello # {_iteration}"});
           
            _logger.LogInformation($"Sent message with id {_iteration}");
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
