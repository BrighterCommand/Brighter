
namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// Represents the configuration for a Google Cloud Pub/Sub Dead Letter Policy (DLQ).
/// This policy is applied to a subscription to define how undeliverable messages should be handled.
/// </summary>
/// <param name="topic">The name of the Dead Letter Topic (DLT) where undeliverable messages will be forwarded.</param>
/// <param name="subscription">The name of the main subscription this DLQ policy is attached to.</param>
public class DeadLetterPolicy(RoutingKey topic, ChannelName subscription)
{
    /// <summary>
    /// Gets or sets the <see cref="RoutingKey"/> for the Dead Letter Topic (DLT) where messages that exceed the max delivery attempts are sent.
    /// This topic must exist on Google Cloud Pub/Sub.
    /// </summary>
    public RoutingKey TopicName { get; set; } = topic;

    /// <summary>
    /// Gets or sets the <see cref="ChannelName"/> of the main subscription that this dead letter policy applies to.
    /// </summary>
    public ChannelName Subscription { get; set; } = subscription;

    /// <summary>
    /// Gets or sets the member field in the main message that identifies the publisher.
    /// This is often used for logging or tracing purposes.
    /// </summary>
    public string? PublisherMember { get; set; }

    /// <summary>
    /// Gets or sets the member field in the main message that identifies the subscriber.
    /// This is often used for logging or tracing purposes.
    /// </summary>
    public string? SubscriberMember { get; set; }

    /// <summary>
    /// Gets or sets the number of seconds the subscriber has to acknowledge a message before Pub/Sub redelivers it.
    /// This value is applied to the **dead-letter subscription** (the subscription on the DLT) if one is created by Brighter.
    /// The default is 60 seconds, which is the Pub/Sub default.
    /// </summary>
    public int AckDeadlineSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the maximum number of times Pub/Sub attempts to deliver a message before
    /// sending it to the Dead Letter Topic.
    /// The value must be between 5 and 100. The default is 10.
    /// </summary>
    public int MaxDeliveryAttempts { get; set; } = 10;
}
