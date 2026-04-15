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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Paramore.Brighter.Validation;

namespace Paramore.Brighter.Extensions.DependencyInjection;

/// <summary>
/// Runs pipeline diagnostic description at host startup. When
/// <see cref="BrighterPipelineValidationOptions.ConsumerOwnsValidation"/> is true,
/// this service is a no-op because <c>ServiceActivatorHostedService</c> handles diagnostics.
/// </summary>
public class BrighterDiagnosticHostedService : IHostedService
{
    private readonly IAmAPipelineDiagnosticWriter _diagnosticWriter;
    private readonly IOptions<BrighterPipelineValidationOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrighterDiagnosticHostedService"/> class.
    /// </summary>
    /// <param name="diagnosticWriter">The diagnostic writer that produces pipeline descriptions.</param>
    /// <param name="options">Validation options controlling whether this service acts or defers to the consumer.</param>
    public BrighterDiagnosticHostedService(
        IAmAPipelineDiagnosticWriter diagnosticWriter,
        IOptions<BrighterPipelineValidationOptions> options)
    {
        _diagnosticWriter = diagnosticWriter;
        _options = options;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_options.Value.ConsumerOwnsValidation)
            return Task.CompletedTask;

        _diagnosticWriter.Describe();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
