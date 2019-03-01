using Microsoft.Extensions.DependencyInjection;
using Polly.Registry;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    public class BrighterOptions : IBrighterOptions
    {
        /// <summary>
        ///     Configures the request context factory. Defaults to <see cref="InMemoryRequestContextFactory" />.
        /// </summary>
        public IAmARequestContextFactory RequestContextFactory { get; set; } = new InMemoryRequestContextFactory();

        /// <summary>
        ///     Configures the polly policy registry.
        /// </summary>
        public IPolicyRegistry<string> PolicyRegistry { get; set; } = new DefaultPolicy();

        /// <summary>
        ///     Configures task queues to send messages.  
        /// </summary>
        public BrighterMessaging BrighterMessaging { get; set; }  

        /// <summary>
        ///     Configures the life time of the Command Processor. Defaults to Singleton.
        /// </summary>
        public ServiceLifetime CommandProcessorLifetime { get; set; } = ServiceLifetime.Singleton;
    }

    public interface IBrighterOptions
    {
        /// <summary>
        ///     Configures the request context factory. Defaults to <see cref="InMemoryRequestContextFactory" />.
        /// </summary>
        IAmARequestContextFactory RequestContextFactory { get; set; }

        /// <summary>
        ///     Configures the polly policy registry.
        /// </summary>
        IPolicyRegistry<string> PolicyRegistry { get; set; }

        /// <summary>
        ///     Configures task queues to send messages. 
        /// </summary>
        BrighterMessaging BrighterMessaging { get; set; }

        /// <summary>
        ///     Configures the life time of the Command Processor. Defaults to Singleton
        /// </summary>
        ServiceLifetime CommandProcessorLifetime { get; set; }
    }
}