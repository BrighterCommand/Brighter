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
        /// We commit processed work (marked as acked or rejected) when a batch size worth of work has been completed
        /// If the batch size is 1, then there is a low risk of offsets not being committed and therefore duplicates appearing
        /// in the stream, but the latency per request and load on the broker increases. As the batch size rises the risk of
        /// a crashing worker process failing to commit a batch that is then represented rises.
        /// </summary>
        public long CommitBatchSize { get; set; }
        
        /// <summary>
        /// Allows you to modify the Kafka client configuration before a consumer is created.
        /// Used to set properties that Brighter does not expose
        /// </summary>
        public Action<ConsumerConfig>? ConfigHook { get; set; }
        
        /// <summary>
        /// Only one consumer in a group can read from a partition at any one time; this preserves ordering
        /// We do not default this value, and expect you to set it
        /// </summary>
        public string? GroupId { get; set; }

        /// <summary>
        /// Default to read only committed messages, change if you want to read uncommited messages. May cause duplicates.
        /// </summary>
        public IsolationLevel IsolationLevel { get; set; }
        
        /// <summary>
        /// How often the consumer needs to poll for new messages to be considered alive, polling greater than this interval triggers a rebalance
        /// Uses Kafka default of 300000
        /// </summary>
        public TimeSpan MaxPollInterval { get; set; } = TimeSpan.FromMilliseconds(300000);


        /// <summary>
        /// How many partitions on this topic?
        /// </summary>
        public int NumPartitions { get; set; }

        /// <summary>
        /// What do we do if there is no offset stored in ZooKeeper for this consumer
        /// AutoOffsetReset.Earliest -  (default) Begin reading the stream from the start
        /// AutoOffsetReset.Latest - Start from now i.e. only consume messages after we start
        /// AutoOffsetReset.Error - Consider it an error to be lacking a reset
        /// </summary>
        public AutoOffsetReset OffsetDefault { get; set; }
        
        /// <summary>
        /// How should we assign partitions to consumers in the group?
        /// Range - Assign to co-localise partitions to consumers in the same group
        /// RoundRobin - Assign partitions to consumers in a round robin fashion
        /// CooperativeSticky - Brighter's default, reduce the number of re-balances by assigning partitions to the consumer that previously owned them
        /// </summary>
        public PartitionAssignmentStrategy PartitionAssignmentStrategy { get; set; }

        /// <summary>
        /// How long before we time out when we are reading the committed offsets back (mainly used for debugging)
        /// </summary>
        public TimeSpan ReadCommittedOffsetsTimeOut { get; set; } = TimeSpan.FromMilliseconds(5000);
        
        /// <summary>
        /// What is the replication factor? How many nodes is the topic copied to on the broker?
        /// </summary>
        public short ReplicationFactor { get; set; }
        
        /// <summary>
        /// If Kafka does not receive a heartbeat from the consumer within this time window, trigger a re-balance
        /// Default is Kafka default of 10s
        /// </summary>
        public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromMilliseconds(10000);

        /// <summary>
        /// 
        /// </summary>
        public TimeSpan SweepUncommittedOffsetsInterval { get; set; } = TimeSpan.FromMilliseconds(30000);
        
        /// <summary>
        /// How long to wait when asking for topic metadata
        /// </summary>
        public TimeSpan TopicFindTimeout { get; set; } = TimeSpan.FromMilliseconds(5000);
        
        /// <inheritdoc />
        public override Type ChannelFactoryType => typeof(ChannelFactory);

        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="subscriptionName">The name. Defaults to the data type's full name.</param>
        /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
        /// <param name="requestType">Type of the data.</param>
        /// <param name="getRequestType">The <see cref="Func{Message, Type}"/> that determines how we map a message to a type. Defaults to returning the <paramref name="requestType"/> if null</param>
        /// <param name="groupId">What is the id of the consumer group that this consumer belongs to; will not process the same partition as others in group</param>
        /// <param name="bufferSize">The number of messages to buffer at any one time, also the number of messages to retrieve at once. Min of 1 Max of 10</param>
        /// <param name="noOfPerformers">The no of threads reading this channel.</param>
        /// <param name="timeOut">The timeout in milliseconds.</param>
        /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelay">The delay the delivery of a requeue message. </param>
        /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="offsetDefault">Where should we begin processing if we cannot find a stored offset</param>
        /// <param name="commitBatchSize">How often should we commit offsets?</param>
        /// <param name="sessionTimeout">What is the heartbeat interval for this consumer, after which Kafka will assume dead and rebalance the consumer group; defaults to 10000ms</param>
        /// <param name="maxPollInterval">How often does the consumer poll for a message to be considered alive, after which Kafka will assume dead and rebalance. Defaults to 30000ms</param>
        /// <param name="sweepUncommittedOffsetsInterval">How often do we commit offsets that have yet to be saved; defaults to 30000</param>
        /// <param name="isolationLevel">Should we read messages that are not on all replicas? May cause duplicates.</param>
        /// <param name="messagePumpType">Is this channel read asynchronously</param>
        /// <param name="numOfPartitions">How many partitions should this topic have - used if we create the topic</param>
        /// <param name="replicationFactor">How many copies of each partition should we have across our broker's nodes - used if we create the topic</param>
        /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
        /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
        /// <param name="emptyChannelDelay">How long to pause when a channel is empty in milliseconds</param>
        /// <param name="channelFailureDelay">How long to pause when there is a channel failure in milliseconds</param>
        /// <param name="partitionAssignmentStrategy">How do partitions get assigned to consumers?</param>
        /// <param name="configHook">Allows you to modify the Kafka client configuration before a consumer is created. Used to set properties that Brighter does not expose</param>
        /// ///
        public KafkaSubscription(
            SubscriptionName subscriptionName,
            ChannelName channelName,
            RoutingKey routingKey,
            Type? requestType = null,
            Func<Message, Type>? getRequestType = null,
            string? groupId = null,
            int bufferSize = 1,
            int noOfPerformers = 1,
            TimeSpan? timeOut = null,
            int requeueCount = -1,
            TimeSpan? requeueDelay = null,
            int unacceptableMessageLimit = 0,
            AutoOffsetReset offsetDefault = AutoOffsetReset.Earliest,
            long commitBatchSize = 10,
            TimeSpan? sessionTimeout = null,
            TimeSpan? maxPollInterval = null,
            TimeSpan? sweepUncommittedOffsetsInterval = null,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            MessagePumpType messagePumpType = MessagePumpType.Unknown,
            int numOfPartitions = 1,
            short replicationFactor = 1,
            IAmAChannelFactory? channelFactory = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create,
            TimeSpan? emptyChannelDelay = null,
            TimeSpan? channelFailureDelay = null,
            PartitionAssignmentStrategy partitionAssignmentStrategy = PartitionAssignmentStrategy.RoundRobin,
            Action<ConsumerConfig>? configHook = null) 
            : base(subscriptionName, channelName, routingKey,  requestType, getRequestType, bufferSize, 
                noOfPerformers, timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory, makeChannels, emptyChannelDelay, channelFailureDelay)
        {
            CommitBatchSize = commitBatchSize;
            GroupId = groupId;
            IsolationLevel = isolationLevel;
            MaxPollInterval = maxPollInterval ?? MaxPollInterval;
            SweepUncommittedOffsetsInterval = sweepUncommittedOffsetsInterval ?? SweepUncommittedOffsetsInterval;
            OffsetDefault = offsetDefault;
            SessionTimeout = sessionTimeout ?? SessionTimeout;
            NumPartitions = numOfPartitions;
            ReplicationFactor = replicationFactor;
            PartitionAssignmentStrategy = partitionAssignmentStrategy;
            
            if (PartitionAssignmentStrategy == PartitionAssignmentStrategy.CooperativeSticky)
                throw new ArgumentOutOfRangeException(nameof(partitionAssignmentStrategy),
                    "CooperativeSticky is not supported for with manual commits, see https://github.com/confluentinc/librdkafka/issues/4059");
            
            ConfigHook = configHook;
        }
    }

    public class KafkaSubscription<T> : KafkaSubscription where T : IRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="subscriptionName">The name. Defaults to the data type's full name.</param>
        /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
        /// <param name="getRequestType">The <see cref="Func{Message, Type}"/> that determines how we map a message to a type. Defaults to returning the <see cref="T"/> if null</param>
        /// <param name="groupId">What is the id of the consumer group that this consumer belongs to; will not process the same partition as others in group</param>
        /// <param name="bufferSize">The number of messages to buffer at any one time, also the number of messages to retrieve at once. Min of 1 Max of 10</param>
        /// <param name="noOfPerformers">The no of threads reading this channel.</param>
        /// <param name="timeOut">The timeout. Defaults to 300 milliseconds.</param>
        /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelay">The delay the delivery of a requeue message. 0 is no delay. Defaults to 0</param>
        /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="offsetDefault">Where should we begin processing if we cannot find a stored offset</param>
        /// <param name="commitBatchSize">How often should we commit offsets?</param>
        /// <param name="sessionTimeout">What is the heartbeat interval for this consumer, after which Kafka will assume dead and rebalance the consumer group; defaults to 10000ms</param>
        /// <param name="maxPollInterval">How often does the consumer poll for a message to be considered alive, after which Kafka will assume dead and rebalance; defaults to 30000ms</param>
        /// <param name="sweepUncommittedOffsetsInterval">How often do we commit offsets that have yet to be saved; defaults to 30000ms</param>
        /// <param name="isolationLevel">Should we read messages that are not on all replicas? May cause duplicates.</param>
        /// <param name="messagePumpType">Is this channel read asynchronously</param>
        /// <param name="numOfPartitions">How many partitions should this topic have - used if we create the topic</param>
        /// <param name="replicationFactor">How many copies of each partition should we have across our broker's nodes - used if we create the topic</param>
        /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
        /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
        /// <param name="emptyChannelDelay">How long to pause when a channel is empty in milliseconds</param>
        /// <param name="channelFailureDelay">How long to pause when there is a channel failure in milliseconds</param>
        /// <param name="partitionAssignmentStrategy">How do partitions get assigned to consumers?</param>
        /// <param name="configHook">Allows you to modify the Kafka client configuration before a consumer is created. Used to set properties that Brighter does not expose</param>
        public KafkaSubscription(SubscriptionName? subscriptionName = null,
            ChannelName? channelName = null,
            RoutingKey? routingKey = null,
            Func<Message, Type>? getRequestType = null,
            string? groupId = null,
            int bufferSize = 1,
            int noOfPerformers = 1,
            TimeSpan? timeOut = null,
            int requeueCount = -1,
            TimeSpan? requeueDelay = null,
            int unacceptableMessageLimit = 0,
            AutoOffsetReset offsetDefault = AutoOffsetReset.Earliest,
            long commitBatchSize = 10,
            TimeSpan? sessionTimeout = null,
            TimeSpan? maxPollInterval = null,
            TimeSpan? sweepUncommittedOffsetsInterval = null,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            MessagePumpType messagePumpType = MessagePumpType.Reactor,
            int numOfPartitions = 1,
            short replicationFactor = 1,
            IAmAChannelFactory? channelFactory = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create,
            TimeSpan? emptyChannelDelay = null,
            TimeSpan? channelFailureDelay = null,
            PartitionAssignmentStrategy partitionAssignmentStrategy = PartitionAssignmentStrategy.RoundRobin,
            Action<ConsumerConfig>? configHook = null) 
            : base(
                subscriptionName ?? new SubscriptionName(typeof(T).FullName!),
                channelName ?? new ChannelName(typeof(T).FullName!), 
                routingKey ?? new RoutingKey(typeof(T).FullName!), 
                typeof(T), 
                getRequestType, 
                groupId, 
                bufferSize, 
                noOfPerformers, 
                timeOut, 
                requeueCount, 
                requeueDelay, 
                unacceptableMessageLimit, 
                offsetDefault, 
                commitBatchSize, 
                sessionTimeout, 
                maxPollInterval, 
                sweepUncommittedOffsetsInterval, 
                isolationLevel, 
                messagePumpType,
                numOfPartitions, 
                replicationFactor, 
                channelFactory, 
                makeChannels,
                emptyChannelDelay, 
                channelFailureDelay, 
                partitionAssignmentStrategy, 
                configHook)
        {
        }
    }
}
