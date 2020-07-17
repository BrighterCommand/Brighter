using System.Collections.Generic;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection
{
    public class ServiceActivatorOptions : BrighterOptions
    {
        public IEnumerable<Connection> Connections { get; set; } = new List<Connection>();
        public IAmAChannelFactory ChannelFactory { get; set; } 
    }
}