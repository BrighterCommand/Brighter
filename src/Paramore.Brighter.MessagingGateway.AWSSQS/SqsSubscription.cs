#region Licence

/* The MIT License (MIT)
Copyright © 2020 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using Amazon.SimpleNotificationService.Model;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    /// <summary>
    /// A subscription for an SQS Consumer.
    /// We will create infrastructure on the basis of Make Channels
    /// Create = topic using routing key name, queue using channel name
    /// Validate = look for topic using routing key name, queue using channel name
    /// Assume = Assume Routing Key is Topic ARN, queue exists via channel name
    /// </summary>
    public class SqsSubscription : Subscription
    {
        /// <summary>
        /// This governs how long, in seconds, a 'lock' is held on a message for one consumer
        /// to process. SQS calls this the VisibilityTimeout
        /// </summary>
        public int LockTimeout { get; }

        /// <summary>
        /// The length of time, in seconds, for which the delivery of all messages in the queue is delayed.
        /// </summary>
        public int DelaySeconds { get; }

        /// <summary>
        /// The length of time, in seconds, for which Amazon SQS retains a message
        /// </summary>
        public int MessageRetentionPeriod { get; }

        /// <summary>
        /// Indicates how we should treat the routing key
        /// TopicFindBy.Arn -> the routing key is an Arn
        /// TopicFindBy.Convention -> The routing key is a name, but use convention to make an Arn for this account
        /// TopicFindBy.Name -> Treat the routing key as a name & use ListTopics to find it (rate limited 30/s)
        /// </summary>
        public TopicFindBy FindTopicBy { get; }

        /// <summary>
        ///  The JSON serialization of the queue's access control policy.
        /// </summary>
        public string IAMPolicy { get; }

        /// <summary>
        /// The policy that controls when we send messages to a DLQ after too many requeue attempts
        /// </summary>
        public RedrivePolicy RedrivePolicy { get; }
        
        /// <summary>
        /// The attributes of the topic. If TopicARN is set we will always assume that we do not
        /// need to create or validate the SNS Topic
        /// </summary>
        public SnsAttributes SnsAttributes { get; }

        /// <summary>
        /// A list of resource tags to use when creating the queue
        /// </summary>
        public Dictionary<string, string> Tags { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="dataType">Type of the data.</param>
        /// <param name="name">The name. Defaults to the data type's full name.</param>
        /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
        /// <param name="noOfPerformers">The no of threads reading this channel.</param>
        /// <param name="bufferSize">The number of messages to buffer at any one time, also the number of messages to retrieve at once. Min of 1 Max of 10</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelayInMilliseconds">The number of milliseconds to delay the delivery of a requeue message for.</param>
        /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="runAsync">Is this channel read asynchronously</param>
        /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
        /// <param name="lockTimeout">What is the visibility timeout for the queue</param>
        /// <param name="delaySeconds">The length of time, in seconds, for which the delivery of all messages in the queue is delayed.</param>
        /// <param name="messageRetentionPeriod">The length of time, in seconds, for which Amazon SQS retains a message</param>
        /// <param name="findTopicBy">Is the Topic an Arn, should be treated as an Arn by convention, or a name</param>
        /// <param name="iAmPolicy">The queue's policy. A valid AWS policy.</param>
        /// <param name="redrivePolicy">The policy that controls when and where requeued messages are sent to the DLQ</param>
        /// <param name="snsAttributes">The attributes of the Topic, either ARN if created, or attributes for creation</param>
        /// <param name="tags">Resource tags to be added to the queue</param>
        /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
        public SqsSubscription(Type dataType,
            SubscriptionName name = null,
            ChannelName channelName = null,
            RoutingKey routingKey = null,
            int noOfPerformers = 1,
            int bufferSize = 1,
            int timeoutInMilliseconds = 300,
            int requeueCount = -1,
            int requeueDelayInMilliseconds = 0,
            int unacceptableMessageLimit = 0,
            bool runAsync = false,
            IAmAChannelFactory channelFactory = null,
            int lockTimeout = 10,
            int delaySeconds = 0,
            int messageRetentionPeriod = 345600,
            TopicFindBy findTopicBy = TopicFindBy.Name,
            string iAmPolicy = null,
            RedrivePolicy redrivePolicy = null,
            SnsAttributes snsAttributes = null,
            Dictionary<string,string> tags = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create
        )
            : base(dataType, name, channelName, routingKey, bufferSize, noOfPerformers, timeoutInMilliseconds, requeueCount, requeueDelayInMilliseconds,
                unacceptableMessageLimit, runAsync, channelFactory, makeChannels)
        {
            LockTimeout = lockTimeout;
            DelaySeconds = delaySeconds;
            MessageRetentionPeriod = messageRetentionPeriod;
            FindTopicBy = findTopicBy;
            IAMPolicy = iAmPolicy;
            RedrivePolicy = redrivePolicy;
            SnsAttributes = snsAttributes;
            Tags = tags;
        }
    }

    /// <summary>
    /// A subscription for an SQS Consumer.
    /// We will create infrastructure on the basis of Make Channels
    /// Create = topic using routing key name, queue using channel name
    /// Validate = look for topic using routing key name, queue using channel name
    /// Assume = Assume Routing Key is Topic ARN, queue exists via channel name
    /// </summary>
    public class SqsSubscription<T> : SqsSubscription where T : IRequest
    {
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="name">The name. Defaults to the data type's full name.</param>
        /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
        /// <param name="noOfPerformers">The no of threads reading this channel.</param>
        /// <param name="bufferSize">The number of messages to buffer at any one time, also the number of messages to retrieve at once. Min of 1 Max of 10</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelayInMilliseconds">The number of milliseconds to delay the delivery of a requeue message for.</param>
        /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="runAsync">Is this channel read asynchronously</param>
        /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
        /// <param name="lockTimeout">What is the visibility timeout for the queue</param>
        /// <param name="delaySeconds">The length of time, in seconds, for which the delivery of all messages in the queue is delayed.</param>
        /// <param name="messageRetentionPeriod">The length of time, in seconds, for which Amazon SQS retains a message</param>
        /// <param name="iAmPolicy">The queue's policy. A valid AWS policy.</param>
        /// <param name="redrivePolicy">The policy that controls when and where requeued messages are sent to the DLQ</param>
        /// <param name="snsAttributes">The attributes of the Topic, either ARN if created, or attributes for creation</param>
        /// <param name="tags">Resource tags to be added to the queue</param>
        /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
        
        public SqsSubscription(SubscriptionName name = null,
            ChannelName channelName = null,
            RoutingKey routingKey = null,
            int noOfPerformers = 1,
            int bufferSize = 1,
            int timeoutInMilliseconds = 300,
            int requeueCount = -1,
            int requeueDelayInMilliseconds = 0,
            int unacceptableMessageLimit = 0,
            bool runAsync = false,
            IAmAChannelFactory channelFactory = null,
            int lockTimeout = 10,
            int delaySeconds = 0,
            int messageRetentionPeriod = 345600,
            TopicFindBy findTopicBy = TopicFindBy.Name,
            string iAmPolicy = null,
            RedrivePolicy redrivePolicy = null,
            SnsAttributes snsAttributes = null,
            Dictionary<string,string> tags = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create
        )
            : base(typeof(T), name, channelName, routingKey, noOfPerformers, bufferSize, timeoutInMilliseconds, requeueCount, requeueDelayInMilliseconds,
                unacceptableMessageLimit, runAsync, channelFactory, lockTimeout, delaySeconds, messageRetentionPeriod,findTopicBy, iAmPolicy,redrivePolicy,
                snsAttributes, tags, makeChannels)
        {
        }
    }
}
