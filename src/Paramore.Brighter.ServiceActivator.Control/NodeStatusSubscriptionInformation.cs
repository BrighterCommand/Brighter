namespace Paramore.Brighter.ServiceActivator.Control;

public record NodeStatusSubscriptionInformation
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
    public int ActivePerformers { get => Performers.Length; }
    
    /// <summary>
    /// Number of expected performers
    /// </summary>
    public int ExpectedPerformers { get; init; }

    /// <summary>
    /// Is this subscription healthy on this node
    /// </summary>
    public bool IsHealthy
    {
        get => ActivePerformers == ExpectedPerformers;
    }
}
