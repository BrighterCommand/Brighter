namespace Paramore.Brighter.MessagingGateway.AWSSQS;

/// <summary>
/// The SQS Message publication
/// </summary>
public class SqsPublication : Publication
{
    /// <summary>
    /// Indicates how we should treat the routing key
    /// QueueFindBy.Url -> the routing key is an url 
    /// TopicFindBy.Name -> Treat the routing key as a name & use GetQueueUrl to find it 
    /// </summary>
    public QueueFindBy FindQueueBy { get; set; } = QueueFindBy.Name;

    /// <summary>
    /// The attributes of the topic. If TopicARNs is set we will always assume that we do not
    /// need to create or validate the SNS Topic
    /// </summary>
    public SqsAttributes? SqsAttributes { get; set; } 

    /// <summary>
    /// If we want to use queue Url and not queues you need to supply the Url to use for any message that you send to us,
    /// as we use the topic from the header to dispatch to an url.
    /// </summary>
    public string? QueueUrl { get; set; }
}
