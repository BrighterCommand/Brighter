using System.Diagnostics;

namespace Paramore.Brighter.ServiceActivator.Control.Events;

public record NodeStatusEvent : IEvent
{
    /// <summary>
    /// The event Id
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The Diagnostics Span
    /// </summary>
    public Activity Span { get; set; }

    /// <summary>
    /// The name of the node running Service Activator
    /// </summary>
    public string NodeName { get; init; }
    
    /// <summary>
    /// The Topics that this node can service
    /// </summary>
    public string[] AvailableTopics { get; init; }
    
    /// <summary>
    /// Information about currently configured subscriptions
    /// </summary>
    public SubscriptionInformation[] Subscriptions { get; init; }
    
    /// <summary>
    /// Is this node Healthy
    /// </summary>
    public bool IsHealthy { get => Subscriptions.Any(s => s.IsHealty != true); }
    
    /// <summary>
    /// The Number of Performers currently running on the Node
    /// </summary>
    public int NumberOfActivePerformers { get => Subscriptions.Sum(s => s.ActivePerformers); }
    
    /// <summary>
    /// Timestamp of Status Event
    /// </summary>
    public DateTime TimeStamp { get; } = DateTime.UtcNow;
    
    /// <summary>
    /// The version of the running process
    /// </summary>
    public string ExecutingAssemblyVersion { get; init; }
}

public record SubscriptionInformation
{
    /// <summary>
    /// Name of Topic
    /// </summary>
    public string TopicName { get; init; }
    
    /// <summary>
    /// The name of all the Performers
    /// </summary>
    public string[] Performers { get; init; }
    
    /// <summary>
    /// Number of currently active performers
    /// </summary>
    public int ActivePerformers { get => Performers.Count(); }
    
    /// <summary>
    /// Number of expected performers
    /// </summary>
    public int ExpectedPerformers { get; init; }

    /// <summary>
    /// Is this subscription healthy on this node
    /// </summary>
    public bool IsHealty
    {
        get => ActivePerformers == ExpectedPerformers;
    }
}
