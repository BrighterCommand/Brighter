using System.Reflection;
using Paramore.Brighter.ServiceActivator.Control.Events;

namespace Paramore.Brighter.ServiceActivator.Control.Extensions;

public static class DispatcherExtensions
{
    public static NodeStatus GetNodeStatus(this IDispatcher dispatcher)
    {
        var state = dispatcher.GetState();
        
        return new NodeStatus()
        {
            AvailableTopics = state.Select(c => c.Name).ToArray(),
            ExecutingAssemblyVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "None",
            NodeName = dispatcher.HostName,
            Subscriptions = state
                .Select(c => new NodeStatusSubscriptionInformation()
                {
                    TopicName = c.Name,
                    Performers = c.Performers.Where(p => p.State == ConsumerState.Open).Select(p => p.Name).ToArray(),
                    ExpectedPerformers = c.ExpectPerformers
                })
                .ToArray()
        };
    }
    
    public static NodeStatusEvent AsEvent(this NodeStatus nodeStatus)
    {
        return new NodeStatusEvent()
        {
            Id = Id.Random,
            AvailableTopics = nodeStatus.AvailableTopics,
            ExecutingAssemblyVersion = nodeStatus.ExecutingAssemblyVersion,
            NodeName = nodeStatus.NodeName,
            IsHealthy = nodeStatus.IsHealthy,
            NumberOfActivePerformers = nodeStatus.NumberOfActivePerformers,
            TimeStamp = nodeStatus.TimeStamp,
            Subscriptions = nodeStatus.Subscriptions
                .Select(c => new SubscriptionInformation()
                {
                    TopicName = c.TopicName,
                    Performers = c.Performers,
                    ExpectedPerformers = c.ExpectedPerformers,
                    ActivePerformers = c.ActivePerformers,
                    IsHealthy = c.IsHealthy
                })
                .ToArray()
        };
    }
}
