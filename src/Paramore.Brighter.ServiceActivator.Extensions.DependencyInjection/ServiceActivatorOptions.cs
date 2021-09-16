using System.Collections.Generic;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection
{
    /// <summary>
    /// Subscriptions used when creating a service activator
    /// </summary>
    public class ServiceActivatorOptions : BrighterOptions
    {
        /// <summary>
        /// An iterator over the subscriptions that this ServiceActivator has
        /// </summary>
        public IEnumerable<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    }
}
