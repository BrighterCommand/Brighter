using Paramore.Brighter.ServiceActivator.Control.Events;

namespace Paramore.Brighter.ServiceActivator.Control.Api.Responses;

public record GetStatusResponse
{
    public GetStatusResponse(NodeStatus nodeStatus)
    {
        NodeName = nodeStatus.NodeName;
        AvailableTopics = nodeStatus.AvailableTopics;
        Subscriptions = nodeStatus.Subscriptions.Select(s => new GetStatusResponseSubscriptionInformation(s))
            .ToArray();
        IsHealthy = nodeStatus.IsHealthy;
        NumberOfActivePerformers = nodeStatus.NumberOfActivePerformers;
        TimeStamp = nodeStatus.TimeStamp;
        ExecutingAssemblyVersion = nodeStatus.ExecutingAssemblyVersion;
    }
    
    /// <summary>
    /// The name of the node running Service Activator
    /// </summary>
    public string NodeName { get; }
    
    /// <summary>
    /// The Topics that this node can service
    /// </summary>
    public string[] AvailableTopics { get; }
    
    /// <summary>
    /// Information about currently configured subscriptions
    /// </summary>
    public GetStatusResponseSubscriptionInformation[] Subscriptions { get; }
    
    /// <summary>
    /// Is this node Healthy
    /// </summary>
    public bool IsHealthy { get; }
    
    /// <summary>
    /// The Number of Performers currently running on the Node
    /// </summary>
    public int NumberOfActivePerformers { get; }
    
    /// <summary>
    /// Timestamp of Status Event
    /// </summary>
    public DateTimeOffset TimeStamp { get; } = DateTimeOffset.Now;
    
    /// <summary>
    /// The version of the running process
    /// </summary>
    public string ExecutingAssemblyVersion { get; }
}
