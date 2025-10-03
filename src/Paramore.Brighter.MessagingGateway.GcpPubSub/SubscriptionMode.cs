namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// Specifies the mode of subscription to be used for consuming messages from a Pub/Sub topic.
/// </summary>
public enum SubscriptionMode
{
    /// <summary>
    /// Uses a **streaming pull** mechanism, where a single, long-lived bidirectional gRPC stream
    /// is maintained for receiving messages. This is generally preferred for high-throughput, low-latency scenarios.
    /// </summary>
    Stream,

    /// <summary>
    /// Uses a traditional **unary pull** mechanism, where the client sends an explicit
    /// request to the service to retrieve a batch of messages. This is less efficient than streaming pull
    /// for continuous consumption.
    /// </summary>
    Pull
}
