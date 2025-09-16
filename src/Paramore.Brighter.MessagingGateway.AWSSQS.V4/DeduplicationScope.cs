namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// For High throughput for FIFO queues
/// See: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/high-throughput-fifo.html
/// </summary>
public enum DeduplicationScope
{
    /// <summary>
    /// The throughput configuration to be applied to message group
    /// </summary>
    MessageGroup,
    
    /// <summary>
    /// The throughput configuration to be applied to the queue
    /// </summary>
    Queue
}
