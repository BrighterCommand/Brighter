﻿using System.Collections.Generic;

namespace Paramore.Brighter
{
    public interface IServiceActivatorOptions
    {
        /// <summary>
        /// Used to create a channel, an abstraction over a message processing pipeline
        /// </summary>
        IAmAChannelFactory ChannelFactory { get; set; }
        
        /// <summary>
        /// The configuration of our inbox
        /// </summary>
        InboxConfiguration InboxConfiguration { get; set; }

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
}
