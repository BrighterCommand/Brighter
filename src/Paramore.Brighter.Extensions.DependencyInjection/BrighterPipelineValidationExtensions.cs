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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Validation;

namespace Paramore.Brighter.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="IBrighterBuilder"/> for opting into pipeline validation
/// and diagnostic description at startup.
/// </summary>
public static class BrighterPipelineValidationExtensions
{
    /// <summary>
    /// Registers pipeline validation services. At startup, registered handler pipelines
    /// are evaluated against validation rules and errors prevent the host from starting.
    /// </summary>
    /// <param name="builder">The Brighter builder.</param>
    /// <returns>The builder, for fluent chaining.</returns>
    public static IBrighterBuilder ValidatePipelines(this IBrighterBuilder builder)
    {
        builder.Services.TryAddSingleton<IAmAPipelineValidator>(sp =>
        {
            var subscriberRegistry = sp.GetService<IAmASubscriberRegistryInspector>()
                ?? (IAmASubscriberRegistryInspector)sp.GetRequiredService<ServiceCollectionSubscriberRegistry>();
            var pipelineBuilder = new PipelineBuilder<IRequest>(subscriberRegistry);
            return new PipelineValidator(pipelineBuilder);
        });

        builder.Services.AddSingleton<IHostedService, BrighterValidationHostedService>();
        builder.Services.AddOptions<BrighterPipelineValidationOptions>();

        return builder;
    }

    /// <summary>
    /// Registers pipeline diagnostic services. At startup, a human-readable description
    /// of all configured pipelines is logged at Information and Debug levels.
    /// </summary>
    /// <param name="builder">The Brighter builder.</param>
    /// <returns>The builder, for fluent chaining.</returns>
    public static IBrighterBuilder DescribePipelines(this IBrighterBuilder builder)
    {
        builder.Services.TryAddSingleton<IAmAPipelineDiagnosticWriter>(sp =>
        {
            var subscriberRegistry = sp.GetService<IAmASubscriberRegistryInspector>()
                ?? (IAmASubscriberRegistryInspector)sp.GetRequiredService<ServiceCollectionSubscriberRegistry>();
            var pipelineBuilder = new PipelineBuilder<IRequest>(subscriberRegistry);
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<PipelineDiagnosticWriter>();
            return new PipelineDiagnosticWriter(logger, pipelineBuilder);
        });

        builder.Services.AddSingleton<IHostedService, BrighterDiagnosticHostedService>();
        builder.Services.AddOptions<BrighterPipelineValidationOptions>();

        return builder;
    }
}
