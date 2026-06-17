#region Licence
/* The MIT License (MIT)
Copyright © 2026 Jonny Olliff-Lee <jonny.ollifflee@gmail.com>

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
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Neuroglia.AsyncApi.IO;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.AsyncAPI
{
    public static class AsyncApiBrighterBuilderExtensions
    {
        private const string NJsonSchemaGeneratorTypeName =
            "Paramore.Brighter.AsyncAPI.NJsonSchema.NJsonSchemaGenerator, Paramore.Brighter.AsyncAPI.NJsonSchema";

        /// <summary>
        /// Registers the AsyncAPI document generator and its dependencies with the Brighter pipeline.
        /// Subscriptions are sourced from <see cref="IAmConsumerOptions"/> (registered by AddConsumers or AddServiceActivator).
        /// Publications are sourced from <see cref="IAmAProducerRegistry"/> (registered by UseExternalBus) and
        /// optionally from <see cref="AsyncApiOptions.SupplementalPublications"/>.
        /// Send-only applications that do not register consumers will produce documents with no receive operations.
        /// </summary>
        /// <param name="builder">The Brighter builder to extend.</param>
        /// <param name="configure">Optional delegate to configure <see cref="AsyncApiOptions"/>.</param>
        /// <returns>The builder for chaining.</returns>
        public static IBrighterBuilder UseAsyncApi(this IBrighterBuilder builder, Action<AsyncApiOptions>? configure = null)
        {
            var services = builder.Services;

            var options = new AsyncApiOptions();
            configure?.Invoke(options);
            services.AddSingleton(options);

            // Register SDK serialization services (IAsyncApiDocumentWriter, IAsyncApiDocumentReader)
            services.AddAsyncApiIO();

            // Register default schema generator via reflection (NJsonSchema package)
            var generatorType = Type.GetType(NJsonSchemaGeneratorTypeName);
            if (generatorType != null)
            {
                services.TryAddSingleton(typeof(IAmASchemaGenerator), generatorType);
            }

            // Validate that a schema generator is available
            var hasCustom = services.Any(sd => sd.ServiceType == typeof(IAmASchemaGenerator));
            if (!hasCustom)
            {
                throw new InvalidOperationException(
                    "No IAmASchemaGenerator implementation is available. " +
                    "Either add a reference to Paramore.Brighter.AsyncAPI.NJsonSchema or register a custom IAmASchemaGenerator before calling UseAsyncApi().");
            }

            services.AddSingleton<IAmAnAsyncApiDocumentGenerator>(sp =>
            {
                var resolvedOptions = sp.GetRequiredService<AsyncApiOptions>();
                var schemaGenerator = sp.GetRequiredService<IAmASchemaGenerator>();
                var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Paramore.Brighter.AsyncAPI");

                // Resolve subscriptions from IAmConsumerOptions (core Brighter)
                var consumerOptions = sp.GetService<IAmConsumerOptions>();
                var subscriptions = consumerOptions?.Subscriptions;
                if (consumerOptions == null)
                {
                    logger.LogDebug("IAmConsumerOptions is not registered; no subscriptions will be included in the AsyncAPI document");
                }

                // Resolve publications from IAmAProducerRegistry (core Brighter)
                var producerRegistry = sp.GetService<IAmAProducerRegistry>();
                var producerPublications = producerRegistry?.Producers?.Select(p => p.Publication).ToList();
                if (producerRegistry == null)
                {
                    logger.LogDebug("IAmAProducerRegistry is not registered; only supplemental publications will be included in the AsyncAPI document");
                }

                // Merge producer registry publications with supplemental publications
                var allPublications = producerPublications;
                if (resolvedOptions.SupplementalPublications != null)
                {
                    if (allPublications != null)
                    {
                        allPublications.AddRange(resolvedOptions.SupplementalPublications);
                    }
                    else
                    {
                        allPublications = resolvedOptions.SupplementalPublications.ToList();
                    }
                }

                return new AsyncApiDocumentGenerator(resolvedOptions, schemaGenerator, subscriptions, allPublications);
            });

            return builder;
        }
    }
}
