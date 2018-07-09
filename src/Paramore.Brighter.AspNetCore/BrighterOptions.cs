using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.AspNetCore
{
    public class BrighterOptions
    {
        /// <summary>
        /// Configures the request context factory. Defaults to <see cref="InMemoryRequestContextFactory"/>.
        /// </summary>
        public IAmARequestContextFactory RequestContextFactory { get; set; } = new InMemoryRequestContextFactory();

        /// <summary>
        /// Configures the policy registry. Set to null to use Brighter's default policy.
        /// </summary>
        public IAmAPolicyRegistry PolicyRegistry { get; set; }

        /// <summary>
        /// Configures task queues. Set to null to not use task queues.
        /// </summary>
        public MessagingConfiguration MessagingConfiguration { get; set; }

        /// <summary>
        /// Configures how the services are injected. Defaults to Transient.
        /// </summary>
        public ServiceLifetime HandlerLifetime { get; set; } = ServiceLifetime.Transient;
    }
}