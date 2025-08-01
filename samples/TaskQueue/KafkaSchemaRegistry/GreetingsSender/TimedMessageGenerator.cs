using System;
using System.Threading;
using System.Threading.Tasks;
using Greetings.Ports.Commands;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;

namespace GreetingsSender
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

            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
            
            DoWork(null);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Kafka Message Generator is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }
        
        private async void DoWork(object state)
        {
            _iteration++;

            var greetingEvent = new GreetingEvent{ Id = Id.Random(), Greeting = $"Hello # {_iteration}"};
            
            await _processor.PostAsync(greetingEvent);

            _logger.LogInformation("Sending message with id {Id} and greeting {Request}", greetingEvent.Id,
                greetingEvent.Greeting);
            
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
