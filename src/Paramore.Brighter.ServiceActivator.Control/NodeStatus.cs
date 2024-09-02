namespace Paramore.Brighter.ServiceActivator.Control;

public record NodeStatus
{
    /// <summary>
    /// The name of the node running Service Activator
    /// </summary>
    public string NodeName { get; init; } = string.Empty;

    /// <summary>
    /// The Topics that this node can service
    /// </summary>
    public string[] AvailableTopics { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Information about currently configured subscriptions
    /// </summary>
    public NodeStatusSubscriptionInformation[] Subscriptions { get; init; } = Array.Empty<NodeStatusSubscriptionInformation>();
    
    /// <summary>
    /// Is this node Healthy
    /// </summary>
    public bool IsHealthy { get => Subscriptions.All(s => s.IsHealthy); }
    
    /// <summary>
    /// The Number of Performers currently running on the Node
    /// </summary>
    public int NumberOfActivePerformers { get => Subscriptions.Sum(s => s.ActivePerformers); }
    
    /// <summary>
    /// Timestamp of Status Event
    /// </summary>
    public DateTimeOffset TimeStamp { get; } = DateTimeOffset.Now;

    /// <summary>
    /// The version of the running process
    /// </summary>
    public string ExecutingAssemblyVersion { get; init; } = string.Empty;
}
