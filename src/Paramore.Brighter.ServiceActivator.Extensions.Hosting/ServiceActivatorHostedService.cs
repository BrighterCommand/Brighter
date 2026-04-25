using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Validation;

namespace Paramore.Brighter.ServiceActivator.Extensions.Hosting
{
    public class ServiceActivatorHostedService : IHostedService
    {
        private readonly ILogger<ServiceActivatorHostedService> _logger;
        private readonly IDispatcher _dispatcher;
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptions<BrighterPipelineValidationOptions> _options;

        /// <summary>
        /// Initializes a new instance of <see cref="ServiceActivatorHostedService"/>.
        /// Optional pipeline validation and diagnostic dependencies are resolved from the
        /// service provider at startup, controlled by <see cref="BrighterPipelineValidationOptions"/>.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="dispatcher">The message dispatcher.</param>
        /// <param name="serviceProvider">The service provider for resolving optional validation dependencies.</param>
        /// <param name="options">Validation options controlling whether this service runs validation before Receive.</param>
        public ServiceActivatorHostedService(
            ILogger<ServiceActivatorHostedService> logger,
            IDispatcher dispatcher,
            IServiceProvider serviceProvider,
            IOptions<BrighterPipelineValidationOptions> options)
        {
            _logger = logger;
            _dispatcher = dispatcher;
            _serviceProvider = serviceProvider;
            _options = options;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting hosted service dispatcher");

            if (_options.Value.ConsumerOwnsValidation)
            {
                var diagnosticWriter = _serviceProvider.GetService<IAmAPipelineDiagnosticWriter>();
                diagnosticWriter?.Describe();

                var validator = _serviceProvider.GetService<IAmAPipelineValidator>();
                if (validator != null)
                {
                    var result = validator.Validate();

                    if (_options.Value.ThrowOnError)
                    {
                        result.ThrowIfInvalid();
                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            _logger.LogError("Pipeline validation error from {Source}: {Message}", error.Source, error.Message);
                        }
                    }

                    foreach (var warning in result.Warnings)
                    {
                        _logger.LogWarning("Pipeline validation warning from {Source}: {Message}", warning.Source, warning.Message);
                    }
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
