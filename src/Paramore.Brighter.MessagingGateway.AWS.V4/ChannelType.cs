namespace Paramore.Brighter.MessagingGateway.AWS.V4;

/// <summary>
/// The Channel type
/// </summary>
public enum ChannelType
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
