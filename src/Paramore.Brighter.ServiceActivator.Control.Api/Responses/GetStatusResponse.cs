using Paramore.Brighter.ServiceActivator.Control.Events;

namespace Paramore.Brighter.ServiceActivator.Control.Api.Responses;

public record GetStatusResponse
{
    public GetStatusResponse(NodeStatusEvent nodeStatusEvent)
    {
        NodeName = nodeStatusEvent.NodeName;
        AvailableTopics = nodeStatusEvent.AvailableTopics;
        Subscriptions = nodeStatusEvent.Subscriptions.Select(s => new GetStatusResponseSubscriptionInformation(s))
            .ToArray();
        IsHealthy = nodeStatusEvent.IsHealthy;
        NumberOfActivePerformers = nodeStatusEvent.NumberOfActivePerformers;
        TimeStamp = nodeStatusEvent.TimeStamp;
        ExecutingAssemblyVersion = nodeStatusEvent.ExecutingAssemblyVersion;
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
    public DateTime TimeStamp { get; } = DateTime.UtcNow;
    
    /// <summary>
    /// The version of the running process
    /// </summary>
    public string ExecutingAssemblyVersion { get; }
}
