using Microsoft.Extensions.DependencyInjection;
using Polly.Registry;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    public class BrighterOptions : IBrighterOptions
    {
        /// <summary>
        /// Used to create a channel, an abstraction over a message processing pipeline
        /// </summary>
        public IAmAChannelFactory ChannelFactory { get; set; }

        /// <summary>
        ///  Configures the life time of the Command Processor. Defaults to Transient.
        /// </summary>
        public ServiceLifetime CommandProcessorLifetime { get; set; } = ServiceLifetime.Transient;

        /// <summary>
        /// Configures the lifetime of the Handlers. Defaults to Scoped.
        /// </summary>
        public ServiceLifetime HandlerLifetime { get; set; } = ServiceLifetime.Transient;

        /// <summary>
        /// Configures the lifetime of mappers. Defaults to Singleton
        /// </summary>
        public ServiceLifetime MapperLifetime { get; set; } = ServiceLifetime.Singleton;

        /// <summary>
        ///     Configures the polly policy registry.
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
        /// Used to create a channel, an abstraction over a message processing pipeline
        /// </summary>
        IAmAChannelFactory ChannelFactory { get; set; }

        /// <summary>
        /// Configures the life time of the Command Processor.
        /// </summary>
        ServiceLifetime CommandProcessorLifetime { get; set; }

        /// <summary>
        /// Configures the lifetime of the Handlers.
        /// </summary>
        ServiceLifetime HandlerLifetime { get; set; }

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
