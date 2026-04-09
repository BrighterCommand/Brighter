#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paramore.Brighter.Validation;

namespace Paramore.Brighter.Extensions.DependencyInjection;

/// <summary>
/// Runs pipeline validation at host startup for non-consumer applications.
/// When <see cref="BrighterPipelineValidationOptions.ConsumerOwnsValidation"/> is true,
/// this service is a no-op because <c>ServiceActivatorHostedService</c> handles validation.
/// Optional dependencies (<see cref="IAmAPipelineDiagnosticWriter"/>) are resolved from
/// <see cref="IServiceProvider"/> at startup because Microsoft.Extensions.DependencyInjection
/// does not support optional constructor injection.
/// </summary>
public class BrighterValidationHostedService : IHostedService
{
    private readonly IOptions<BrighterPipelineValidationOptions> _options;
    private readonly IAmAPipelineValidator _validator;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BrighterValidationHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrighterValidationHostedService"/> class.
    /// </summary>
    /// <param name="options">Validation options controlling whether this service acts or defers to the consumer.</param>
    /// <param name="validator">The pipeline validator.</param>
    /// <param name="serviceProvider">The service provider for resolving optional dependencies.</param>
    /// <param name="logger">The logger.</param>
    public BrighterValidationHostedService(
        IOptions<BrighterPipelineValidationOptions> options,
        IAmAPipelineValidator validator,
        IServiceProvider serviceProvider,
        ILogger<BrighterValidationHostedService> logger)
    {
        _options = options;
        _validator = validator;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_options.Value.ConsumerOwnsValidation)
            return Task.CompletedTask;

        var result = _validator.Validate();

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

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
