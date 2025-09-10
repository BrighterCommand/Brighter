namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// Specifies whether the FIFO queue throughput quota applies to the entire queue or per message group.
/// Valid values are perQueue and perMessageGroupId.
/// To enable high throughput for a FIFO queue, set this attribute to perMessageGroupId and set the
/// DeduplicationScope attribute to messageGroup.
/// </summary>
public enum FifoThroughputLimit
{
    PerQueue,
    PerMessageGroupId
}
