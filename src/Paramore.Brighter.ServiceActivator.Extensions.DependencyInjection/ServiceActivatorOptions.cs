using System.Collections.Generic;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection
{
    public interface IServiceActivatorOptions
    {
        /// <summary>
        /// Used to create a channel, an abstraction over a message processing pipeline
        /// </summary>
        IAmAChannelFactory ChannelFactory { get; set; }

        /// <summary>
        /// An iterator over the subscriptions that this ServiceActivator has
        /// </summary>
        IEnumerable<Subscription> Subscriptions { get; set; }

        /// <summary>
        /// Ensures that we use a Command Processor with as scoped lifetime, to allow scoped handlers
        /// to take a dependency on it alongside other scoped dependencies such as an EF Core DbContext
        ///  Otherwise the CommandProcessor is a singleton.
        /// </summary>
        bool UseScoped { get; set; }
    }

    /// <summary>
    /// Subscriptions used when creating a service activator
    /// </summary>
    public class ServiceActivatorOptions : BrighterOptions
    {
        /// <summary>
        /// Used to create a channel, an abstraction over a message processing pipeline
        /// </summary>
        public IAmAChannelFactory ChannelFactory { get; set; }
        
        /// <summary>
        /// The configuration of our inbox
        /// </summary>
        public InboxConfiguration InboxConfiguration { get; set; }

        /// <summary>
        /// An iterator over the subscriptions that this ServiceActivator has
        /// </summary>
        public IEnumerable<Subscription> Subscriptions { get; set; } = new List<Subscription>();

        /// <summary>
        /// Ensures that we use a Command Processor with as scoped lifetime, to allow scoped handlers
        /// to take a dependency on it alongside other scoped dependencies such as an EF Core. DbContext
        ///  Otherwise the CommandProcessor is a singleton.
        /// </summary>
        public bool UseScoped { get; set; } = false;
    }
}
