namespace Paramore.Brighter.MessagingGateway.RocketMQ;

/// <summary>
/// Represents a RocketMQ message publication configuration for Brighter integration.
/// This class provides basic message attributes like tag for topic categorization.
/// </summary>
public class RocketPublication : Publication
{
    /// <summary>
    /// Gets or sets the message tag for filtering in RocketMQ topics.
    /// Tags help categorize messages within the same topic for selective consumption.
    /// </summary>
    public string? Tag { get; set; }
}

/// <summary>
/// Strongly-typed RocketMQ publication for Brighter command/message handling.
/// Implements RocketMQ's low-latency messaging pattern with type safety.
/// </summary>
/// <typeparam name="T">The request type, must implement Brighter's IRequest interface</typeparam>
public class RocketPublication<T> : RocketPublication
    where T : class, IRequest
{
    /// <summary>
    /// Initializes a new instance of the RocketPublication class.
    /// Sets the request type for message routing and handler resolution.
    /// </summary>
    public RocketPublication()
    {
        RequestType = typeof(T);
    }
}
