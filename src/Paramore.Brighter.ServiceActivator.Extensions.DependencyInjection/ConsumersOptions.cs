using System;
using System.Collections.Generic;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection
{
    /// <summary>
    /// Subscriptions used when creating a service activator
    /// </summary>
    public class ConsumersOptions : BrighterOptions, IAmConsumerOptions
    {
        /// <summary>
        /// Used to create a channel if no channel factory is provided on a subscription, an abstraction over a message processing pipeline
        /// </summary>
        public  IAmAChannelFactory? DefaultChannelFactory { get; set; }

        /// <summary>
        /// The configuration of our inbox
        /// </summary>
        public InboxConfiguration InboxConfiguration { get; set; } = new();

        /// <summary>
        /// An iterator over the subscriptions that this ServiceActivator has
        /// </summary>
        public IEnumerable<Subscription> Subscriptions { get; set; } = new List<Subscription>();

        /// <summary>
        /// How long the Dispatcher waits, when it is disposed at shutdown, for the pumps to drain their
        /// in-flight message before disposing the mapper/transform factories. Increase it for consumers with
        /// long-running handlers (for example video processing) so a message in progress can finish rather than
        /// be interrupted by teardown. On expiry the interrupted message is left un-acknowledged for redelivery.
        /// Defaults to 10 seconds.
        /// </summary>
        public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(10);

    }
}
