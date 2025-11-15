using Paramore.Brighter.Observability;

namespace Paramore.Brighter.MessagingGateway.RocketMQ;

/// <summary>
/// Specifies the type of RocketMQ topic to use for message publication.
/// Different topic types support different messaging patterns and delivery guarantees.
/// </summary>
public enum TopicType
{
    /// <summary>
    /// Standard topic type for normal message delivery.
    /// Messages are delivered in the order they are received by the broker, but not guaranteed to be consumed in order.
    /// Suitable for most general-purpose messaging scenarios.
    /// </summary>
    Normal,
    
    /// <summary>
    /// Delay topic type for scheduled message delivery.
    /// Messages are delivered to consumers after a specified delay time.
    /// Useful for scenarios like retry logic, scheduled notifications, or time-based workflows.
    /// </summary>
    Delay,
    
    /// <summary>
    /// FIFO (First-In-First-Out) topic type for strict message ordering.
    /// Guarantees that messages are consumed in the exact order they were produced.
    /// Essential for scenarios requiring strict sequence processing, such as transaction processing or state machines.
    /// Note: FIFO topics may have different performance characteristics compared to normal topics.
    /// </summary>
    Fifo
}

/// <summary>
/// Represents a RocketMQ message publication configuration for Brighter integration.
/// This class provides basic message attributes like tag for topic categorization.
/// </summary>
public class RocketMqPublication : Publication
{
    /// <summary>
    /// Gets or sets the message tag for filtering in RocketMQ topics.
    /// Tags help categorize messages within the same topic for selective consumption.
    /// </summary>
    /// <value>
    /// A string representing the message tag. If null or empty, no tag filtering is applied.
    /// Tags should follow RocketMQ naming conventions (typically alphanumeric with underscores).
    /// </value>
    /// <remarks>
    /// <para>
    /// Tags are used by consumers to filter messages within a topic. For example, a consumer can subscribe
    /// to "topicA" with tag "important" to only receive messages tagged as important.
    /// </para>
    /// <para>
    /// Common use cases include:
    /// <list type="bullet">
    /// <item><description>Priority-based routing (e.g., "high", "medium", "low")</description></item>
    /// <item><description>Message type categorization (e.g., "create", "update", "delete")</description></item>
    /// <item><description>Environment segregation (e.g., "production", "staging", "test")</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public string? Tag { get; set; }
    
    /// <summary>
    /// Gets or sets the message tag for filtering in RocketMQ topics.
    /// Tags help categorize messages within the same topic for selective consumption.
    /// </summary>
    /// <value>
    /// A string representing the message tag. If null or empty, no tag filtering is applied.
    /// Tags should follow RocketMQ naming conventions (typically alphanumeric with underscores).
    /// </value>
    /// <remarks>
    /// <para>
    /// Tags are used by consumers to filter messages within a topic. For example, a consumer can subscribe
    /// to "topicA" with tag "important" to only receive messages tagged as important.
    /// </para>
    /// <para>
    /// Common use cases include:
    /// <list type="bullet">
    /// <item><description>Priority-based routing (e.g., "high", "medium", "low")</description></item>
    /// <item><description>Message type categorization (e.g., "create", "update", "delete")</description></item>
    /// <item><description>Environment segregation (e.g., "production", "staging", "test")</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public InstrumentationOptions? Instrumentation { get; set; }
    
    /// <summary>
    /// Gets or sets the type of RocketMQ topic to use for message publication.
    /// </summary>
    /// <value>
    /// The <see cref="TopicType"/> enum value specifying the topic behavior. Defaults to <see cref="TopicType.Normal"/>.
    /// </value>
    /// <remarks>
    /// <para>
    /// The topic type determines the message delivery characteristics:
    /// <list type="bullet">
    /// <item><description><see cref="TopicType.Normal"/>: Standard delivery with best-effort ordering</description></item>
    /// <item><description><see cref="TopicType.Delay"/>: Messages delivered after a configured delay</description></item>
    /// <item><description><see cref="TopicType.Fifo"/>: Strict first-in-first-out message ordering</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Choose the appropriate topic type based on your application requirements:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// Use <see cref="TopicType.Normal"/> for most scenarios where strict ordering is not required
    /// and you need maximum throughput.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Use <see cref="TopicType.Delay"/> for scenarios requiring scheduled message delivery,
    /// such as retry mechanisms or time-based workflows.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Use <see cref="TopicType.Fifo"/> when message order is critical, such as in financial transactions
    /// or state machine transitions where sequence matters.
    /// </description>
    /// </item>
    /// </list>
    /// </para>
    /// <para>
    /// Note: Different topic types may have different performance characteristics and resource requirements.
    /// </para>
    /// </remarks>
    public TopicType TopicType { get; set; } = TopicType.Normal;
}

/// <summary>
/// Strongly-typed RocketMQ publication for Brighter command/message handling.
/// Implements RocketMQ's low-latency messaging pattern with type safety.
/// </summary>
/// <typeparam name="T">The request type, must implement Brighter's IRequest interface</typeparam>
public class RocketMqPublication<T> : RocketMqPublication
    where T : class, IRequest
{
    /// <summary>
    /// Initializes a new instance of the RocketPublication class.
    /// Sets the request type for message routing and handler resolution.
    /// </summary>
    public RocketMqPublication()
    {
        RequestType = typeof(T);
    }
}
