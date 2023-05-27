using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.FeatureSwitch;
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
        public IAmAFeatureSwitchRegistry FeatureSwitchRegistry { get; set; } = null;

        /// <summary>
        /// Configures the lifetime of the Handlers. Defaults to Scoped.
        /// </summary>
        public ServiceLifetime HandlerLifetime { get; set; } = ServiceLifetime.Transient;

        /// <summary>
        /// Configures the inbox to de-duplicate requests; will default to in-memory inbox if not set.
        /// </summary>
        public InboxConfiguration InboxConfiguration { get; set; } = new InboxConfiguration();

        /// <summary>
        /// Configures the lifetime of mappers. Defaults to Singleton
        /// </summary>
        public ServiceLifetime MapperLifetime { get; set; } = ServiceLifetime.Singleton;

        /// <summary>
        /// Configures the polly policy registry.
        /// </summary>
        public IPolicyRegistry<string> PolicyRegistry { get; set; } = new DefaultPolicy();

        /// <summary>
        /// Configures the request context factory. Defaults to <see cref="InMemoryRequestContextFactory" />.
        /// </summary>
        public IAmARequestContextFactory RequestContextFactory { get; set; } = new InMemoryRequestContextFactory();

        /// <summary>
        /// Configures the lifetime of any transformers. Defaults to Singleton
        /// </summary>
        public ServiceLifetime TransformerLifetime { get; set; } = ServiceLifetime.Singleton;
        
        
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
        IAmAFeatureSwitchRegistry FeatureSwitchRegistry { get; set; }
        
         /// <summary>
        /// Configures the lifetime of the Handlers.
        /// </summary>
        ServiceLifetime HandlerLifetime { get; set; }

         /// <summary>
         /// Configures the inbox to de-duplicate requests; will default to in-memory inbox if not set.
         /// </summary>
         InboxConfiguration InboxConfiguration { get; set; }

        /// <summary>
        /// Configures the lifetime of mappers. 
        /// </summary>
        ServiceLifetime MapperLifetime { get; set; }

        /// <summary>
        ///  Configures the polly policy registry.
        /// </summary>
        IPolicyRegistry<string> PolicyRegistry { get; set; }

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
