using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Control.Events;
using Paramore.Brighter.ServiceActivator.Control.Hosting;
using Paramore.Brighter.ServiceActivator.Control.Mappers;

namespace Paramore.Brighter.ServiceActivator.Control.Extensions;

public static class ControlExtensions
{
    public static IBrighterBuilder AddControl(this IBrighterBuilder builder)
    {

        builder.Services.AddSingleton<IAmAMessageMapper<NodeStatusEvent>, NodeStatusEventMessageMapper>();
        
        builder.Services.AddHostedService<HeartbeatHostedService>();
        
        return builder;
    }
}
