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
using System.Collections.Generic;
using System.Linq;
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
    /// <param name="enabled">When false, the method is a no-op — no services are registered. Defaults to true.</param>
    /// <returns>The builder, for fluent chaining.</returns>
    public static IBrighterBuilder ValidatePipelines(this IBrighterBuilder builder, bool enabled = true, bool throwOnError = true)
    {
        if (!enabled) return builder;

        builder.Services.Configure<BrighterPipelineValidationOptions>(o => o.ThrowOnError = throwOnError);

        builder.Services.TryAddSingleton<IAmAPipelineValidator>(sp =>
        {
            var subscriberRegistry = sp.GetService<IAmASubscriberRegistryInspector>()
                ?? (IAmASubscriberRegistryInspector)sp.GetRequiredService<ServiceCollectionSubscriberRegistry>();
            var pipelineBuilder = new PipelineBuilder<IRequest>(subscriberRegistry);

            var publications = ResolvePublications(sp);
            var subscriptions = ResolveSubscriptions(sp);
            var consumerSpecs = sp.GetServices<ISpecification<Subscription>>();
            var consumerSpecList = consumerSpecs.Any() ? consumerSpecs : null;

            return new PipelineValidator(pipelineBuilder, publications, subscriptions, consumerSpecList);
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
    /// <param name="enabled">When false, the method is a no-op — no services are registered. Defaults to true.</param>
    /// <returns>The builder, for fluent chaining.</returns>
    public static IBrighterBuilder DescribePipelines(this IBrighterBuilder builder, bool enabled = true)
    {
        if (!enabled) return builder;
        builder.Services.TryAddSingleton<IAmAPipelineDiagnosticWriter>(sp =>
        {
            var subscriberRegistry = sp.GetService<IAmASubscriberRegistryInspector>()
                ?? (IAmASubscriberRegistryInspector)sp.GetRequiredService<ServiceCollectionSubscriberRegistry>();
            var pipelineBuilder = new PipelineBuilder<IRequest>(subscriberRegistry);
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<PipelineDiagnosticWriter>();

            var publications = ResolvePublications(sp);
            var subscriptions = ResolveSubscriptions(sp);
            var mapperRegistryBuilder = sp.GetService<ServiceCollectionMessageMapperRegistryBuilder>();
            var mapperRegistry = mapperRegistryBuilder != null
                ? ServiceCollectionExtensions.MessageMapperRegistry(sp)
                : null;

            return new PipelineDiagnosticWriter(logger, pipelineBuilder, mapperRegistry, publications, subscriptions);
        });

        builder.Services.AddSingleton<IHostedService, BrighterDiagnosticHostedService>();
        builder.Services.AddOptions<BrighterPipelineValidationOptions>();

        return builder;
    }

    private static IEnumerable<Publication>? ResolvePublications(IServiceProvider sp)
    {
        var producerRegistry = sp.GetService<IAmAProducerRegistry>();
        var publications = producerRegistry?.Producers
            .Select(p => p.Publication)
            .ToList();
        return publications is { Count: > 0 } ? publications : null;
    }

    private static IEnumerable<Subscription>? ResolveSubscriptions(IServiceProvider sp)
    {
        var consumerOptions = sp.GetService<IAmConsumerOptions>();
        var subscriptions = consumerOptions?.Subscriptions?.ToList();
        return subscriptions is { Count: > 0 } ? subscriptions : null;
    }
}
