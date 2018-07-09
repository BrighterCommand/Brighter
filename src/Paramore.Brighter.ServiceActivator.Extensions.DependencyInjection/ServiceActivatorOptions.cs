using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection
{
    public class ServiceActivatorOptions : BrighterOptions
    {
        public IEnumerable<Connection> Connections { get; set; } = new List<Connection>();
        public IAmAChannelFactory ChannelFactory { get; set; } 
        public ServiceLifetime MapperLifetime { get; set; } = ServiceLifetime.Transient;
    }
}