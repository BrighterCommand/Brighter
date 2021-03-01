using System.Collections.Generic;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection
{
    public class ServiceActivatorOptions : BrighterOptions
    {
        public IEnumerable<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    }
}
