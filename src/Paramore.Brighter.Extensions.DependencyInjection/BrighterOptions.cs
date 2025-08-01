using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.FeatureSwitch;
using Paramore.Brighter.Observability;
using Polly.Registry;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    public class BrighterOptions : IBrighterOptions
    {
        /// <summary>
        ///  Configures the life time of the Command Processor. Defaults to Transient.
        /// </summary>
        public ServiceLifetime CommandProcessorLifetime { get; set; } = ServiceLifetime.Transient;

        /// <summary>
        /// Do we support feature switching? In which case please supply an initialized feature switch registry
        /// </summary>
        /// <returns></returns>
        public IAmAFeatureSwitchRegistry? FeatureSwitchRegistry { get; set; } = null;

        /// <summary>
        /// Configures the lifetime of the Handlers. Defaults to Scoped.
        /// </summary>
        public ServiceLifetime HandlerLifetime { get; set; } = ServiceLifetime.Transient;

        /// <summary>
        /// Configures how verbose our instrumentation is
        /// InstrumentationOptions.None - no instrumentation
        /// InstrumentationOptions.RequestInformation - just the request id, request type and operation
        /// InstrumentationOptions.RequestBody - the request body
        /// InstrumentationOptions.RequestContext - the request context
        /// InstrumentationOptions.All - all of the above
        /// </summary>
        public InstrumentationOptions InstrumentationOptions { get; set; }

        /// <summary>
        /// Configures the lifetime of mappers. Defaults to transit 
        /// </summary>
        public ServiceLifetime MapperLifetime { get; set; } = ServiceLifetime.Transient;

        /// <inheritdoc />
        [Obsolete("Migrate to ResiliencePipeline")]
        public IPolicyRegistry<string>? PolicyRegistry { get; set; } = new DefaultPolicy();

        /// <inheritdoc />
        public ResiliencePipelineRegistry<string>? ResiliencePipelineRegistry { get; set; }

        /// <summary>
        /// Configures the request context factory. Defaults to <see cref="InMemoryRequestContextFactory" />.
        /// </summary>
        public IAmARequestContextFactory RequestContextFactory { get; set; } = new InMemoryRequestContextFactory();

        /// <summary>
        /// Configures the lifetime of any transformers. Defaults to Singleton
        /// </summary>
        public ServiceLifetime TransformerLifetime { get; set; } = ServiceLifetime.Transient;
    }

    public interface IBrighterOptions
    {
        /// <summary>
        /// Configures the life time of the Command Processor.
        /// </summary>
        ServiceLifetime CommandProcessorLifetime { get; set; }

        /// <summary>
        /// Do we support feature switching? In which case please supply an initialized feature switch registry
        /// </summary>
        /// <returns></returns>
        IAmAFeatureSwitchRegistry? FeatureSwitchRegistry { get; set; }
        
         /// <summary>
        /// Configures the lifetime of the Handlers.
        /// </summary>
        ServiceLifetime HandlerLifetime { get; set; }
         
        /// <summary>
        /// What depth of instrumentation do we need
        /// InstrumentationOptions.None - no instrumentation
        /// InstrumentationOptions.RequestInformation - just the request id, request type and operation
        /// InstrumentationOptions.RequestBody - the request body
        /// InstrumentationOptions.RequestContext - the request context
        /// InstrumentationOptions.All - all of the above
        /// </summary> 
        InstrumentationOptions InstrumentationOptions { get; set; } 

        /// <summary>
        /// Configures the lifetime of mappers. 
        /// </summary>
        ServiceLifetime MapperLifetime { get; set; }

        /// <summary>
        /// [Obsolete] Configure the legacy policy registry.
        /// </summary>
        /// <value>
        /// The policy registry containing resilience policies. Returns <c>null</c> if no policies are configured.
        /// </value>
        /// <remarks>
        /// This property is obsolete and will be removed in a future version. 
        /// Migrate to <see cref="ResiliencePipelineRegistry"/> for new resilience implementations.
        /// </remarks>
        [Obsolete("Migrate to ResiliencePipeline", error: false)]
        IPolicyRegistry<string>? PolicyRegistry { get; set; }
        
        /// <summary>
        /// Configure the registry of resilience pipelines.
        /// </summary>
        /// <value>
        /// The registry containing named resilience pipeline instances. Returns <c>null</c> if no pipelines are configured.
        /// </value>
        /// <remarks>
        /// Use this registry to retrieve pre-configured resilience pipelines by name. 
        /// This replaces the obsolete <see cref="PolicyRegistry"/> property for modern resilience implementations.
        /// </remarks>
        ResiliencePipelineRegistry<string>? ResiliencePipelineRegistry { get; set; }

        /// <summary>
        ///     Configures the request context factory. Defaults to <see cref="InMemoryRequestContextFactory" />.
        /// </summary>
        IAmARequestContextFactory RequestContextFactory { get; set; }
        
        /// <summary>
        /// Configures the lifetime of any transformers.
        /// </summary>
        ServiceLifetime TransformerLifetime { get; set; }

   }
}
