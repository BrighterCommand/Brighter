using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Validation;

namespace Paramore.Brighter.ServiceActivator.Extensions.Hosting
{
    public class ServiceActivatorHostedService : IHostedService
    {
        private readonly ILogger<ServiceActivatorHostedService> _logger;
        private readonly IDispatcher _dispatcher;
        private readonly IAmAPipelineValidator? _validator;
        private readonly IAmAPipelineDiagnosticWriter? _diagnosticWriter;

        public ServiceActivatorHostedService(ILogger<ServiceActivatorHostedService> logger, IDispatcher dispatcher)
            : this(logger, dispatcher, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ServiceActivatorHostedService"/> with optional
        /// pipeline validation and diagnostic support.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="dispatcher">The message dispatcher.</param>
        /// <param name="validator">Optional pipeline validator; when present, validation runs before Receive.</param>
        /// <param name="diagnosticWriter">Optional diagnostic writer; when present, pipeline descriptions are logged before Receive.</param>
        public ServiceActivatorHostedService(
            ILogger<ServiceActivatorHostedService> logger,
            IDispatcher dispatcher,
            IAmAPipelineValidator? validator,
            IAmAPipelineDiagnosticWriter? diagnosticWriter)
        {
            _logger = logger;
            _dispatcher = dispatcher;
            _validator = validator;
            _diagnosticWriter = diagnosticWriter;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting hosted service dispatcher");

            _diagnosticWriter?.Describe();

            if (_validator != null)
            {
                var result = _validator.Validate();
                result.ThrowIfInvalid();

                foreach (var warning in result.Warnings)
                {
                    _logger.LogWarning("Pipeline validation warning from {Source}: {Message}", warning.Source, warning.Message);
                }
            }

            _dispatcher.Receive();

            var completionSource = new TaskCompletionSource<IDispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
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
