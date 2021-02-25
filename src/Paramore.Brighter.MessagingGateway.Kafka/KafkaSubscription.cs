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
using Confluent.Kafka;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    public class KafkaSubscription : Subscription
    {
          /// <summary>
        /// Only one consumer in a group can read from a partition at any one time; this preserves ordering
        /// We do not default this value, and expect you to set it
        /// </summary>
        public string GroupId { get; set; }

        /// <summary>
        /// If Kafka does not receive a heartbeat from the consumer within this time window, trigger a re-balance
        /// Default is Kafka default of 10s
        /// </summary>
        public int SessionTimeoutMs { get; set; } = 10000;

        /// <summary>
        /// How often the consumer needs to poll for new messages to be considered alive, polling greater than this interval triggers a rebalance
        /// Uses Kafka default of 300000
        /// </summary>
        public int MaxPollIntervalMs { get; set; } = 300000;

        /// <summary>
        /// Default to read only committed messages, change if you want to read uncommited messages. May cause duplicates.
        /// </summary>
        public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;
        
        /// <summary>
        /// What do we do if there is no offset stored in ZooKeeper for this consumer
        /// AutoOffsetReset.Earlist -  (default) Begin reading the stream from the start
        /// AutoOffsetReset.Latest - Start from now i.e. only consume messages after we start
        /// AutoOffsetReset.Error - Consider it an error to be lacking a reset
        /// </summary>
        public AutoOffsetReset OffsetDefault { get; set; } = AutoOffsetReset.Earliest;

        /// <summary>
        /// We commit processed work (marked as acked or rejected) when a batch size worth of work has been completed
        /// If the batch size is 1, then there is a low risk of offsets not being committed and therefore duplicates appearing
        /// in the stream, but the latency per request and load on the broker increases. As the batch size rises the risk of
        /// a crashing worker process failing to commit a batch that is then represented rises.
        /// </summary>
        public long CommitBatchSize { get; set; } = 10;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="dataType">Type of the data.</param>
        /// <param name="name">The name. Defaults to the data type's full name.</param>
        /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
        /// <param name="groupId">What is the id of the consumer group that this consumer belongs to; will not process the same partition as others in group</param>
        /// <param name="bufferSize">The number of messages to buffer at any one time, also the number of messages to retrieve at once. Min of 1 Max of 10</param>
        /// <param name="noOfPerformers">The no of threads reading this channel.</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelayInMilliseconds">The number of milliseconds to delay the delivery of a requeue message for.</param>
        /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="offsetDefault">Where should we begin processing if we cannot find a stored offset</param>
        /// <param name="commitBatchSize">How often should we commit offsets?</param>
        /// <param name="sessionTimeoutMs">What is the heartbeat interval for this consumer, after which Kafka will assume dead and rebalance the consumer group</param>
        /// <param name="maxPollIntervalMs">How often does the consumer poll for a message to be considered alive, after which Kafka will assume dead and rebalance</param>
        /// <param name="isolationLevel">Should we read messages that are not on all replicas? May cause duplicates.</param>
        /// <param name="isAsync">Is this channel read asynchronously</param>
        /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
        /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
        public KafkaSubscription (
            Type dataType, 
            SubscriptionName name = null, 
            ChannelName channelName = null, 
            RoutingKey routingKey = null,
            string groupId = null,
            int bufferSize = 1, 
            int noOfPerformers = 1, 
            int timeoutInMilliseconds = 300, 
            int requeueCount = -1, 
            int requeueDelayInMilliseconds = 0, 
            int unacceptableMessageLimit = 0, 
            AutoOffsetReset offsetDefault = AutoOffsetReset.Earliest,
            long commitBatchSize = 10,
            int sessionTimeoutMs = 10000,
            int maxPollIntervalMs = 300000, 
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            bool isAsync = false, 
            IAmAChannelFactory channelFactory = null, 
            OnMissingChannel makeChannels = OnMissingChannel.Create) 
            : base(dataType, name, channelName, routingKey, bufferSize, noOfPerformers, timeoutInMilliseconds, requeueCount, 
                requeueDelayInMilliseconds, unacceptableMessageLimit, isAsync, channelFactory, makeChannels)
        {
            CommitBatchSize = commitBatchSize;
            GroupId = groupId;
            IsolationLevel = isolationLevel;
            MaxPollIntervalMs = maxPollIntervalMs;
            OffsetDefault = offsetDefault;
            SessionTimeoutMs = sessionTimeoutMs;
        }
    }

    public class KafkaSubscription<T> : KafkaSubscription where T : IRequest
    {
         /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="name">The name. Defaults to the data type's full name.</param>
        /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
        /// <param name="groupId">What is the id of the consumer group that this consumer belongs to; will not process the same partition as others in group</param>
        /// <param name="bufferSize">The number of messages to buffer at any one time, also the number of messages to retrieve at once. Min of 1 Max of 10</param>
        /// <param name="noOfPerformers">The no of threads reading this channel.</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelayInMilliseconds">The number of milliseconds to delay the delivery of a requeue message for.</param>
        /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="offsetDefault">Where should we begin processing if we cannot find a stored offset</param>
        /// <param name="commitBatchSize">How often should we commit offsets?</param>
        /// <param name="sessionTimeoutMs">What is the heartbeat interval for this consumer, after which Kafka will assume dead and rebalance the consumer group</param>
        /// <param name="maxPollIntervalMs">How often does the consumer poll for a message to be considered alive, after which Kafka will assume dead and rebalance</param>
        /// <param name="isolationLevel">Should we read messages that are not on all replicas? May cause duplicates.</param>
        /// <param name="isAsync">Is this channel read asynchronously</param>
        /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
        /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
        public KafkaSubscription(
            SubscriptionName name = null, 
            ChannelName channelName = null, 
            RoutingKey routingKey = null, 
            string groupId = null,
            int bufferSize = 1, 
            int noOfPerformers = 1, 
            int timeoutInMilliseconds = 300, 
            int requeueCount = -1, 
            int requeueDelayInMilliseconds = 0, 
            int unacceptableMessageLimit = 0, 
            AutoOffsetReset offsetDefault = AutoOffsetReset.Earliest,
            long commitBatchSize = 10,
            int sessionTimeoutMs = 10000,
            int maxPollIntervalMs = 300000, 
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            bool isAsync = false, 
            IAmAChannelFactory channelFactory = null, 
            OnMissingChannel makeChannels = OnMissingChannel.Create) 
            : base(typeof(T), name, channelName, routingKey, groupId, bufferSize, noOfPerformers, timeoutInMilliseconds, requeueCount, 
                requeueDelayInMilliseconds, unacceptableMessageLimit, offsetDefault, commitBatchSize, sessionTimeoutMs, maxPollIntervalMs, isolationLevel, isAsync, channelFactory, makeChannels)
        {
        }
    }
}
