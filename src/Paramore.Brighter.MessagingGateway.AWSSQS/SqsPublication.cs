using System;

namespace Paramore.Brighter.MessagingGateway.AWSSQS;

/// <summary>
/// The SQS Message publication
/// </summary>
public class SqsPublication : Publication
{
    /// <summary>
    /// Creates a new SQS Publication for a point-to-point channel
    /// </summary>
    /// <param name="channelName">The <see cref="ChannelName"/> of the queue that we want to send messages to</param>
    /// <param name="queueAttributes">The <see cref="SqsAttributes"/> for the queue</param>
    /// <param name="findQueueBy">How should we look for the queue. If you set the "queueUrl" you MUST set this to <see cref="QueueFindBy.Url"/></param>
    /// <param name="queueUrl">The Url for the queue if it was created outside Brighter. If you set this, set findQueueBy to <see cref="QueueFindBy.Url"/> you must set this</param>
    /// <param name="makeChannels">A <see cref="OnMissingChannel"/> value: Whether wwe should create the queue, if missing or just validate it</param>
    public SqsPublication(ChannelName? channelName = null,  SqsAttributes? queueAttributes = null, QueueFindBy findQueueBy = QueueFindBy.Name,  string? queueUrl = null, OnMissingChannel makeChannels = OnMissingChannel.Create)
    {
        if (findQueueBy == QueueFindBy.Name && ChannelName.IsNullOrEmpty(channelName))
            throw new ArgumentException("If you use QueueFindBy.Name, you must supply a channel name", nameof(channelName));

        if (findQueueBy == QueueFindBy.Url && string.IsNullOrEmpty(queueUrl))
            throw new ArgumentException("If you use QueueFindBy.Url, you must supply a url for the queue", nameof(queueUrl));
        
        ChannelName = channelName;
        ChannelType = ChannelType.PointToPoint;
        FindQueueBy = findQueueBy;
        MakeChannels = makeChannels;
        QueueUrl = queueUrl;
        QueueAttributes = queueAttributes ?? SqsAttributes.Empty;
    }
    
    /// <summary>
    /// Gets the <see cref="ChannelName"/> we use for this channel. This will be used as the name for the queue
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
    public ChannelType ChannelType { get; }
    
   /// <summary>
   /// How should we find the Sqs queue?
   /// <see cref="QueueFindBy.Name"/> (default) use the <see cref="SqsPublication.ChannelName"/> to look up the queue
   /// <see cref="QueueFindBy.Url"/> use the <see cref="SqsPublication.QueueUrl"/> as the reference for the queue
   /// </summary>
    public QueueFindBy FindQueueBy { get; set; }
    
    /// <summary>
    /// The <see cref="SqsAttributes"/> that describe the queue.
    ///</summary>
    ///<remarks>
    /// Use this if you do not have a <see cref="QueueUrl"/> and intend
    /// to create the queue on the fly.
    /// </remarks>    
    public SqsAttributes? QueueAttributes { get; }
    
    /// <summary>
    /// The <a href="https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-queue-message-identifiers.html">AWS queue Url</a>
    /// </summary>
    /// <remarks>
    /// If we want to use a queue QUrl  and not a queue name in the attributes, then you need to supply the Url to use for any message that you send,
    /// as we use the topic from the header to dispatch to an url.
    /// </remarks>
    public string? QueueUrl { get; init; }

}
