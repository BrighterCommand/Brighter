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
        private readonly TimedOutboxSweeperOptions _options;
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<TimedOutboxSweeper>();
        private Timer _timer;
        
        public TimedOutboxSweeper (IServiceScopeFactory serviceScopeFactory, TimedOutboxSweeperOptions options)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _options = options;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            s_logger.LogInformation("Outbox Sweeper Service is starting.");

            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(_options.TimerInterval));

            return Task.CompletedTask;
        }

        private void DoWork(object state)
        {
            s_logger.LogInformation("Outbox Sweeper looking for unsent messages");

            var scope = _serviceScopeFactory.CreateScope();
            IAmAnOutboxViewer <Message> outbox = scope.ServiceProvider.GetService<IAmAnOutboxViewer<Message>>();
            IAmACommandProcessor commandProcessor = scope.ServiceProvider.GetService<IAmACommandProcessor>();

            var outBoxSweeper = new OutboxSweeper(
                milliSecondsSinceSent:_options.MinimumMessageAge, 
                outbox:outbox, 
                commandProcessor:commandProcessor);

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
