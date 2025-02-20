namespace Paramore.Brighter.MessagingGateway.RocketMQ;

/// <summary>
/// The RocketMQ publication
/// </summary>
public class RocketPublication : Publication
{
    /// <summary>
    /// The rocket message tag
    /// </summary>
    public string? Tag { get; set; }
}
