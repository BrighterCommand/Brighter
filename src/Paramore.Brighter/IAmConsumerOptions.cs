using System;
using System.Collections.Generic;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter
{
    public interface IAmConsumerOptions
    {
        /// <summary>
        /// Used to create a channel, an abstraction over a message processing pipeline
        /// </summary>
        IAmAChannelFactory? DefaultChannelFactory { get; set; }
        
        /// <summary>
        /// The configuration of our inbox
        /// </summary>
        InboxConfiguration InboxConfiguration { get; set; }

        /// <summary>
        /// An iterator over the subscriptions that this ServiceActivator has
        /// </summary>
        IEnumerable<Subscription> Subscriptions { get; set; }

        /// <summary>
        /// How detailed should the instrumentation of the Dispatcher operations be
        /// </summary>
        InstrumentationOptions InstrumentationOptions { get; set; }

        /// <summary>
        /// How long the Dispatcher waits, when it is disposed at shutdown, for the pumps to drain their
        /// in-flight message before disposing the mapper/transform factories. Increase it for consumers with
        /// long-running handlers (for example video processing) so a message in progress can finish rather than
        /// be interrupted by teardown. On expiry the interrupted message is left un-acknowledged for redelivery.
        /// Defaults to 10 seconds.
        /// </summary>
        TimeSpan ShutdownTimeout { get; set; }
    }
}
