using System.Diagnostics;

namespace Paramore.Brighter.ServiceActivator.Control.Events;

public record NodeStatusEvent : IEvent
{
    /// <summary>
    /// The event Id
    /// </summary>
    public Id Id { get; set; } = null!;

    /// <summary>
    /// The Diagnostics Span
    /// </summary>
    public Activity Span { get; set; } = Activity.Current!;

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
    public SubscriptionInformation[] Subscriptions { get; init; } = Array.Empty<SubscriptionInformation>();

    /// <summary>
    /// Is this node Healthy
    /// </summary>
    public bool IsHealthy { get; init; } = false;

    /// <summary>
    /// The Number of Performers currently running on the Node
    /// </summary>
    public int NumberOfActivePerformers { get; init; } = 0;
    
    /// <summary>
    /// Timestamp of Status Event
    /// </summary>
    public DateTimeOffset TimeStamp { get; init; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// The version of the running process
    /// </summary>
    public string ExecutingAssemblyVersion { get; init; } = string.Empty;
}

public record SubscriptionInformation
{
    /// <summary>
    /// Name of Topic
    /// </summary>
    public string TopicName { get; init; } = string.Empty;

    /// <summary>
    /// The name of all the Performers
    /// </summary>
    public string[] Performers { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Number of currently active performers
    /// </summary>
    public int ActivePerformers { get; init; }

    /// <summary>
    /// Number of expected performers
    /// </summary>
    public int ExpectedPerformers { get; init; }

    /// <summary>
    /// Is this subscription healthy on this node
    /// </summary>
    public bool IsHealthy { get; init; }
}
