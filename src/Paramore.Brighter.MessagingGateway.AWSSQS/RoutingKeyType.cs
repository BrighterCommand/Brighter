namespace Paramore.Brighter.MessagingGateway.AWSSQS;

/// <summary>
/// The routing key type
/// </summary>
public enum RoutingKeyType
{
    /// <summary>
    /// Use the Pub/Sub for routing key, aka SNS
    /// </summary>
    PubSub,

    /// <summary>
    /// Use point-to-point for routing key, aka SQS 
    /// </summary>
    PointToPoint
}
