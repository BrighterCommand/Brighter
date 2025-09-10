using System;
using System.Collections.Generic;

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// Represents an SQS queue definition.
/// We use this from both an <see cref="SqsPublication"/> and <see cref="SqsSubscription{T}"/> as either can create a queue.
/// </summary>
public class SqsAttributes
{
    /// <summary>
    /// Creates a new instance of the <see cref="SqsAttributes"/> class.
    /// </summary>
    /// <param name="lockTimeout">What is the visibility timeout for the queue</param>
    /// <param name="timeOut">The ReceiveMessageWaitTimeout.  How long to wait if nothing can be read from the queue. Default is 0, short polling. Range is 0-20s.</param>
    /// <param name="delaySeconds">The length of time, in seconds, for which the delivery of all messages in the queue is delayed.</param>
    /// <param name="messageRetentionPeriod">The length of time, in seconds, for which Amazon SQS retains a message</param>
    /// <param name="iamPolicy">The queue's policy. A valid AWS policy.</param>
    /// <param name="redrivePolicy">The policy that controls when and where requeued messages are sent to the DLQ</param>
    /// <param name="tags">Resource tags to be added to the queue</param>
    /// <param name="rawMessageDelivery">The indication of Raw Message Delivery setting is enabled or disabled</param>
    /// <param name="type">The SQS Type</param>
    /// <param name="contentBasedDeduplication">Enables or disable content-based deduplication</param>
    /// <param name="deduplicationScope">Specifies whether message deduplication occurs at the message group or queue level</param>
    /// <param name="fifoThroughputLimit">Specifies whether the FIFO queue throughput quota applies to the entire queue or per message group</param>
   
    public SqsAttributes(
        TimeSpan? lockTimeout = null,
        TimeSpan? delaySeconds = null,
        TimeSpan? timeOut = null,
        TimeSpan? messageRetentionPeriod = null,
        string? iamPolicy = null,
        bool rawMessageDelivery = true,
        RedrivePolicy? redrivePolicy = null,
        Dictionary<string, string>? tags = null,
        SqsType type = SqsType.Standard,
        bool contentBasedDeduplication = true,
        DeduplicationScope? deduplicationScope = null,
        FifoThroughputLimit? fifoThroughputLimit = null
        )
    {
        TimeOut = ValidateTimeSpan(timeOut, 0, 20, 0);
        LockTimeout = ValidateTimeSpan(lockTimeout, Convert.ToInt32(TimeOut.Value.TotalSeconds), 43200, 30); // Default: 30 seconds
        DelaySeconds = ValidateTimeSpan(delaySeconds, 0, 900, 0); // Default: 0 seconds
        MessageRetentionPeriod = ValidateTimeSpan(messageRetentionPeriod, 0, 1209600, 345600); // Default: 4 days

        ContentBasedDeduplication = contentBasedDeduplication;
        DeduplicationScope = deduplicationScope;
        FifoThroughputLimit = fifoThroughputLimit;
        IamPolicy = iamPolicy;
        RawMessageDelivery = rawMessageDelivery;
        RedrivePolicy = redrivePolicy;
        Type = type;
        Tags = tags;
    }

    /// <summary>
    /// The length of time, in seconds, for which the delivery of all messages in the queue is delayed.
    /// </summary>
    public TimeSpan DelaySeconds { get; }

    /// <summary>
    /// The length of time, in seconds, for which Amazon SQS retains a message
    /// </summary>
    public TimeSpan MessageRetentionPeriod { get; }
    
    /// <summary>
    /// Enables or disable content-based deduplication, for Fifo queues.
    /// </summary>
    public bool ContentBasedDeduplication { get; }

    /// <summary>
    /// Specifies whether message deduplication occurs at the message group or queue level.
    /// This configuration is used for high throughput for FIFO queues configuration
    /// </summary>
    public DeduplicationScope? DeduplicationScope { get; }
    
    /// <summary>
    /// Creates an empty <see cref="SqsAttributes"/> instance.
    /// </summary>
    /// <remarks>This will have default values that allow it to create a queue successfully; useful when you don't want to alter defaults</remarks>
    public static SqsAttributes Empty { get; } = new();

    /// <summary>
    /// Specifies whether the FIFO queue throughput quota applies to the entire queue or per message group
    /// This configuration is used for high throughput for FIFO queues configuration
    /// </summary>
    public FifoThroughputLimit? FifoThroughputLimit { get; }

    /// <summary>
    ///  The JSON serialization of the queue's access control policy.
    /// </summary>
    public string? IamPolicy { get; }
    
    /// <summary>
    /// This governs how long, in seconds, a 'lock' is held on a message for one consumer
    /// to process. SQS calls this the VisibilityTimeout
    /// </summary>
    public TimeSpan LockTimeout { get; }

    /// <summary>
    /// Indicate that the Raw Message Delivery setting is enabled or disabled
    /// </summary>
    public bool RawMessageDelivery { get; }

    /// <summary>
    /// The policy that controls when we send messages to a DLQ after too many requeue attempts
    /// </summary>
    public RedrivePolicy? RedrivePolicy { get; }
    
    /// <summary>
    /// The AWS SQS type.
    /// </summary>
    public SqsType Type { get; }

    /// <summary>
    /// A list of resource tags to use when creating the queue
    /// </summary>
    public Dictionary<string, string>? Tags { get; }

    /// <summary>
    /// Gets the timeout that we use to infer that nothing could be read from the channel i.e. is empty
    /// or busy
    /// </summary>
    /// <value>The timeout</value>
    public TimeSpan? TimeOut { get; }

    private static TimeSpan ValidateTimeSpan(TimeSpan? span, int min, int max, int defaultValue)
    {
        int actualSeconds = 0;
        if (span is null)
        {
            actualSeconds = defaultValue;
        }
        else
        {
            int requestedSeconds = Convert.ToInt32(span!.Value.TotalSeconds);

            if (requestedSeconds < min || requestedSeconds > max)
                actualSeconds = defaultValue;
            else
                actualSeconds = requestedSeconds;
        }

        return TimeSpan.FromSeconds(actualSeconds);
    }
}
