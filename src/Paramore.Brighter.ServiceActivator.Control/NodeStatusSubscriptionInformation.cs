namespace Paramore.Brighter.ServiceActivator.Control;

public record NodeStatusSubscriptionInformation
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
