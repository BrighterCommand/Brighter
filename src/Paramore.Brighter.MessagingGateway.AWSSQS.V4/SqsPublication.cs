using System;

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// The SQS Message publication
/// </summary>
public class SqsPublication : Publication
{
    /// <summary>
    /// Creates a new SQS Publication for a point-to-point channel
    /// </summary>
    public SqsPublication()
    {
        
    }
    
    /// <summary>
    /// Creates a new SQS Publication for a point-to-point channel
    /// </summary>
    /// <param name="channelName">The <see cref="ChannelName"/> of the queue that we want to send messages to.  Use The tUrl for the queue if it was created outside Brighter. If you set this, set findQueueBy to <see cref="QueueFindBy.Url"/> </param>
    /// <param name="queueAttributes">The <see cref="SqsAttributes"/> for the queue</param>
    /// <param name="findQueueBy">How should we look for the queue. If you set the "queueUrl" you MUST set this to <see cref="QueueFindBy.Url"/></param>
    /// <param name="makeChannels">A <see cref="OnMissingChannel"/> value: Whether wwe should create the queue, if missing or just validate it</param>
    public SqsPublication(ChannelName channelName, SqsAttributes? queueAttributes = null, QueueFindBy findQueueBy = QueueFindBy.Name, OnMissingChannel makeChannels = OnMissingChannel.Create)
    {
        if (ChannelName.IsNullOrEmpty(channelName))
        {
            throw new ArgumentException("You must supply a channel name", nameof(channelName));
        }

        ChannelName = channelName;
        ChannelType = ChannelType.PointToPoint;
        FindQueueBy = findQueueBy;
        MakeChannels = makeChannels;
        QueueAttributes = queueAttributes ?? SqsAttributes.Empty;
    }
    
    /// <summary>
    /// Gets the <see cref="ChannelName"/> we use for this channel. This will be used as the name for the queue
    /// If <see cref="FindQueueBy"/> is set to <see cref="QueueFindBy.Name"/> then we will use this to look up the queue
    /// If <see cref="FindQueueBy"/> is set to <see cref="QueueFindBy.Url"/> then we will assume this is a Url of an existing queue
    /// See <a href="https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-queue-message-identifiers.html" /> for the AWS queue Url
    /// </summary>
    /// <remarks>Note that an <see cref="SqsPublication"/> is point-to-point so there is no <see cref="RoutingKey"/> here for a topic.
    /// If you meant to subscribe to an SNS Topic, use <see cref="SnsPublication"/>
    /// </remarks>
    /// <value>The name.</value>
    public ChannelName? ChannelName { get; set; }

    /// <summary>
    /// The <see cref="ChannelType"/> of the channel, either point-to-point or publish-subscribe
    /// </summary>
    /// <remarks>
    /// If you don't use SNS, you are point-to-point, if you do, you are publish-subscribe
    /// </remarks>
    public ChannelType ChannelType { get; set; } = ChannelType.PointToPoint;

    /// <summary>
    /// How should we find the Sqs queue?
    /// <see cref="QueueFindBy.Name"/> (default) use the <see cref="SqsPublication.ChannelName"/> to look up the queue
    /// <see cref="QueueFindBy.Url"/> use the <see cref="SqsPublication.QueueUrl"/> as the reference for the queue
    /// </summary>
    public QueueFindBy FindQueueBy { get; set; } = QueueFindBy.Name;
    
    /// <summary>
    /// The <see cref="SqsAttributes"/> that describe the queue.
    ///</summary>
    ///<remarks>
    /// Use this if you do not have a <see cref="QueueUrl"/> and intend
    /// to create the queue on the fly.
    /// </remarks>    
    public SqsAttributes QueueAttributes { get; set; } =  SqsAttributes.Empty;
}

/// <summary>
/// The SQS Message publication
/// </summary>
public class SqsPublication<T> : SqsPublication 
    where T: class, IRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SqsPublication{T}"/> class.
    /// </summary>
    public SqsPublication()
    {
        RequestType = typeof(T);
    }
}
