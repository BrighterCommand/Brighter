namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// AWS offer two types of SQS.
/// For more information see
/// https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-queue-types.html
/// </summary>
public enum SqsType
{
    /// <summary>
    /// Standard queues support a very high, nearly unlimited number of API calls per second per
    /// action (SendMessage, ReceiveMessage, or DeleteMessage). This high throughput makes them
    /// ideal for use cases that require processing large volumes of messages quickly, such as
    /// real-time data streaming or large-scale applications. While standard queues scale
    /// automatically with demand, it's essential to monitor usage patterns to ensure optimal
    /// performance, especially in regions with higher workloads.
    /// </summary>
    Standard,

    /// <summary>
    /// FIFO (First-In-First-Out) queues have all the capabilities of the standard queues,
    /// but are designed to enhance messaging between applications when the order of operations
    /// and events is critical, or where duplicates can't be tolerated.
    /// The most important features of FIFO queues are FIFO (First-In-First-Out) delivery and
    /// exactly-once processing:
    /// <list type="bullet">
    ///     <item>
    ///         <description>
    ///             The order in which messages are sent and received is strictly preserved and
    ///             a message is delivered once and remains unavailable until a consumer processes
    ///             and deletes it.
    ///         </description>
    ///      </item>
    ///     <item>
    ///         <description>
    ///             Duplicates aren't introduced into the queue.
    ///         </description>
    ///      </item>
    /// </list>
    /// </summary>
    Fifo
}
