using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.ServiceActivator;

namespace Paramore.Brighter.HostedService
{
    public class ServiceActivatorHostedService : IHostedService
    {
        private readonly IApplicationLifetime _appLifetime;
        private readonly ILogger<ServiceActivatorHostedService> _logger;
        private readonly IDispatcher _dispatcher;

        public ServiceActivatorHostedService(ILogger<ServiceActivatorHostedService> logger,
            IApplicationLifetime appLifetime,
            IDispatcher dispatcher)
        {
            _logger = logger;
            _appLifetime = appLifetime;
            _dispatcher = dispatcher;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("StartAsync method called.");
            _appLifetime.ApplicationStarted.Register(OnStarted);
            _appLifetime.ApplicationStopping.Register(OnStopping);
            _appLifetime.ApplicationStopped.Register(OnStopped);

            _logger.LogInformation("Starting dispatcher");
            _dispatcher.Receive();

            var completionSource = new TaskCompletionSource<IDispatcher>();
            completionSource.SetResult(_dispatcher);
            return completionSource.Task;
        }


        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("StopAsync method called.");

            _logger.LogInformation("Stopping dispatcher");
            return _dispatcher.End();
        }

        private void OnStarted()
        {
            _logger.LogInformation("OnStarted method called.");

            // Post-startup code goes here  
        }

        private void OnStopping()
        {
            _logger.LogInformation("OnStopping method called.");

            // On-stopping code goes here  
        }

        private void OnStopped()
        {
            _logger.LogInformation("OnStopped method called.");

            // Post-stopped code goes here  
        }
    }
}