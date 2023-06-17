using System.Reflection;
using Paramore.Brighter.ServiceActivator.Control.Events;

namespace Paramore.Brighter.ServiceActivator.Control.Extensions;

public static class DispatcherExtensions
{
    public static NodeStatusEvent GetNodeStatusEvent(this IDispatcher dispatcher)
    {
        var state = dispatcher.GetState();
        
        return new NodeStatusEvent()
        {
            Id = Guid.NewGuid(),
            AvailableTopics = state.Select(c => c.Name).ToArray(),
            ExecutingAssemblyVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "None",
            NodeName = dispatcher.HostName,
            Subscriptions = state
                .Select(c => new SubscriptionInformation()
                {
                    TopicName = c.Name,
                    Performers = c.Performers.Where(p => p.State == ConsumerState.Open).Select(p => p.Name).ToArray(),
                    ExpectedPerformers = c.ExpectPerformers
                })
                .ToArray()
        };
    }
}
