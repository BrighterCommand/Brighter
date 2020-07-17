using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.ServiceActivator.Extensions.Hosting
{
    public class ServiceActivatorHostedService : IHostedService
    {
        private readonly ILogger<ServiceActivatorHostedService> _logger;
        private readonly IDispatcher _dispatcher;

        public ServiceActivatorHostedService(ILogger<ServiceActivatorHostedService> logger, IDispatcher dispatcher)
        {
            _logger = logger;
            _dispatcher = dispatcher;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting hosted service dispatcher");
            _dispatcher.Receive();

            var completionSource = new TaskCompletionSource<IDispatcher>();
            completionSource.SetResult(_dispatcher);

            return completionSource.Task;
        }


        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping hosted service dispatcher");
            return _dispatcher.End();
        }
    }
}
