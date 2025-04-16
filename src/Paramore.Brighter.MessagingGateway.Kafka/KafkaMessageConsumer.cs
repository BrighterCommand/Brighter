#region Licence
/* The MIT License (MIT)
Copyright © 2015 Wayne Hunsley <whunsley@gmail.com>

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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    /// <inheritdoc cref="Paramore.Brighter.IAmAMessageConsumer" />
    /// <summary>
    /// Class KafkaMessageConsumer is an implementation of <see cref="IAmAMessageConsumer"/>
    /// and provides the facilities to consume messages from a Kafka broker for a topic
    /// in a consumer group.
    /// A Kafka Message Consumer can create topics, depending on the options chosen.
    /// We store an offset when a message is acknowledged, the message pump does this after successfully invoking a handler.
    /// We commit offsets when the batch size is reached, or the sweeper decides it is too long between commits.
    /// This dual strategy prevents low traffic topics having batches that are 'pending' for long periods, causing a risk that the consumer
    /// will end before committing its offsets.
    /// </summary>
    public class KafkaMessageConsumer : KafkaMessagingGateway, IAmAMessageConsumer
    {
        private IConsumer<string, byte[]> _consumer;
        private readonly KafkaMessageCreator _creator;
        private readonly ConsumerConfig _consumerConfig;
        private List<TopicPartition> _partitions = new List<TopicPartition>();
        private readonly ConcurrentBag<TopicPartitionOffset> _offsetStorage = new ConcurrentBag<TopicPartitionOffset>();
        private readonly long _maxBatchSize;
        private readonly int _readCommittedOffsetsTimeoutMs;
        private DateTime _lastFlushAt = DateTime.UtcNow;
        private readonly TimeSpan _sweepUncommittedInterval;
        private readonly SemaphoreSlim _flushToken = new SemaphoreSlim(1, 1);
        private bool _disposedValue;

        /// <summary>
        /// Constructs a KafkaMessageConsumer using Confluent's Consumer Builder. We set up callbacks to handle assigned, revoked or lost partitions as
        /// well as errors. We handle storing and committing offsets, using a batch strategy to commit, with a sweeper thread to prevent partially complete
        /// batches lingering beyond a timeout threshold.
        /// </summary>
        /// <param name="configuration">The configuration tells us how to connect to the Broker. Required.</param>
        /// <param name="routingKey">The routing key is a the Kafka Topic to consume. Required.</param>
        /// <param name="groupId">The id of the consumer group we belong to. Required.</param>
        /// <param name="offsetDefault">When connecting to a stream, and we have not offset stored, where do we begin reading?
        /// Earliest - beginning of the stream; latest - anything after we connect. Defaults to Earliest</param>
        /// <param name="sessionTimeoutMs">If we don't send a heartbeat within this interval, the broker terminates our session. Defaults to 1000ms</param>
        /// <param name="maxPollIntervalMs">Maximum interval between consumer polls, Failing to poll at this interval marks the client as failed triggering a
        /// re-balance of the group. Defaults to 1000ms.  Note that we set the Confluent clients auto store offsets and auto commit to false and handle this
        /// within Brighter. We store an offset following an Ack, and we commit offsets at a batch or sweeper interval, whichever is first.</param>
        /// <param name="isolationLevel">Affects reading of transactionally written messages. Committed only reads those committed to all nodes,
        /// uncommitted reads messages that have been written to *some* node. Defaults to ReadCommitted</param>
        /// <param name="commitBatchSize">What size does a batch grow before we write commits that we have stored. Defaults to 10. If a consumer crashes,
        /// uncommitted offsets will not have been written and will be processed by the consumer group again. Conversely a low batch size increases writes
        /// and lowers performance.</param>
        /// <param name="sweepUncommittedOffsetsIntervalMs">The sweeper ensures that partially complete batches, particularly on low throughput queues
        /// will be written. It runs after the interval and commits anything currently in the store and not committed. Defaults to 30000</param>
        /// <param name="readCommittedOffsetsTimeoutMs">Timeout when reading the committed offsets, used when closing a consumer to log where it reached.
        /// Defaults to 5000</param>
        /// <param name="numPartitions">If we are creating missing infrastructure, How many partitions should the topic have. Defaults to 1</param>
        /// <param name="partitionAssignmentStrategy">What is the strategy for assigning partitions to consumers?</param>
        /// <param name="replicationFactor">If we are creating missing infrastructure, how many in-sync replicas do we need. Defaults to 1</param>
        /// <param name="topicFindTimeoutMs">If we are checking for the existence of the topic, what is the timeout. Defaults to 10000ms</param>
        /// <param name="makeChannels">Should we create infrastructure (topics) where it does not exist or check. Defaults to Create</param>
        /// <exception cref="ConfigurationException">Throws an exception if required parameters missing</exception>
        public KafkaMessageConsumer(
            KafkaMessagingGatewayConfiguration configuration,
            RoutingKey routingKey,
            string groupId,
            AutoOffsetReset offsetDefault = AutoOffsetReset.Earliest,
            int sessionTimeoutMs = 10000,
            int maxPollIntervalMs = 10000,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            long commitBatchSize = 10,
            int sweepUncommittedOffsetsIntervalMs = 30000,
            int readCommittedOffsetsTimeoutMs = 5000,
            int numPartitions = 1,
            PartitionAssignmentStrategy partitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky,
            short replicationFactor = 1,
            int topicFindTimeoutMs = 10000,
            OnMissingChannel makeChannels = OnMissingChannel.Create
            )
        {
            if (configuration is null)
            {
                throw new ConfigurationException("You must set a KafkaMessagingGatewayConfiguration to connect to a broker");
            }
            
            if (routingKey is null)
            {
                throw new ConfigurationException("You must set a RoutingKey as the Topic for the consumer");
            }
            
            if (groupId is null)
            {
                throw new ConfigurationException("You must set a GroupId for the consumer");
            }
            
            Topic = routingKey;

            _clientConfig = new ClientConfig
            {
                BootstrapServers = string.Join(",", configuration.BootStrapServers), 
                ClientId = configuration.Name,
                Debug = configuration.Debug,
                SaslMechanism = configuration.SaslMechanisms.HasValue ? (Confluent.Kafka.SaslMechanism?)((int)configuration.SaslMechanisms.Value) : null,
                SaslKerberosPrincipal = configuration.SaslKerberosPrincipal,
                SaslUsername = configuration.SaslUsername,
                SaslPassword = configuration.SaslPassword,
                SecurityProtocol = configuration.SecurityProtocol.HasValue ? (Confluent.Kafka.SecurityProtocol?)((int) configuration.SecurityProtocol.Value) : null,
                SslCaLocation = configuration.SslCaLocation
            };
            
            
            // We repeat properties because copying them from the ClientConfig modifies the ClientConfig in place 
            _consumerConfig = new ConsumerConfig()
            {
                BootstrapServers = string.Join(",", configuration.BootStrapServers), 
                ClientId = configuration.Name,
                Debug = configuration.Debug,
                SaslMechanism = configuration.SaslMechanisms.HasValue ? (Confluent.Kafka.SaslMechanism?)((int)configuration.SaslMechanisms.Value) : null,
                SaslKerberosPrincipal = configuration.SaslKerberosPrincipal,
                SaslUsername = configuration.SaslUsername,
                SaslPassword = configuration.SaslPassword,
                SecurityProtocol = configuration.SecurityProtocol.HasValue ? (Confluent.Kafka.SecurityProtocol?)((int) configuration.SecurityProtocol.Value) : null,
                SslCaLocation = configuration.SslCaLocation,
                GroupId = groupId,
                AutoOffsetReset = offsetDefault,
                SessionTimeoutMs = sessionTimeoutMs,
                MaxPollIntervalMs = maxPollIntervalMs,
                EnablePartitionEof = true,
                AllowAutoCreateTopics = false, //We will do this explicit always so as to allow us to set parameters for the topic
                IsolationLevel = isolationLevel,
                //We commit the last offset for acknowledged requests when a batch of records has been processed. 
                EnableAutoOffsetStore = false,
                EnableAutoCommit = false,
                // https://www.confluent.io/blog/cooperative-rebalancing-in-kafka-streams-consumer-ksqldb/
                PartitionAssignmentStrategy = partitionAssignmentStrategy,
            };

            _maxBatchSize = commitBatchSize;
            _sweepUncommittedInterval = TimeSpan.FromMilliseconds(sweepUncommittedOffsetsIntervalMs);
            _readCommittedOffsetsTimeoutMs = readCommittedOffsetsTimeoutMs;

            _consumer = new ConsumerBuilder<string, byte[]>(_consumerConfig)
                .SetPartitionsAssignedHandler((consumer, list) =>
                {
                    var partitions = list.Select(p => $"{p.Topic} : {p.Partition.Value}");
                    
                    s_logger.LogInformation("Partition Added {Channels}", String.Join(",", partitions));
                    
                    _partitions.AddRange(list);
                })
                .SetPartitionsRevokedHandler((consumer, list) =>
                {
                    //We should commit any offsets we have stored for these partitions
                    CommitOffsetsFor(list);
                    
                    var revokedPartitionInfo = list.Select(tpo => $"{tpo.Topic} : {tpo.Partition}").ToList();
                    
                    s_logger.LogInformation("Partitions for consumer revoked {Channels}", string.Join(",", revokedPartitionInfo));
                    
                    _partitions = _partitions.Where(tp => list.All(tpo => tpo.TopicPartition != tp)).ToList();
                })
                .SetPartitionsLostHandler((consumer, list) =>
                {
                    var lostPartitions = list.Select(tpo => $"{tpo.Topic} : {tpo.Partition}").ToList();
                    
                    s_logger.LogInformation("Partitions for consumer lost {Channels}", string.Join(",", lostPartitions));
                    
                    _partitions = _partitions.Where(tp => list.All(tpo => tpo.TopicPartition != tp)).ToList();
                })
                .SetErrorHandler((consumer, error) =>
                {
                    var hasFatalError = error.IsFatal;
                    
                    if (hasFatalError ) 
                        s_logger.LogError("Code: {ErrorCode}, Reason: {ErrorMessage}, Fatal: {FatalError}", error.Code, error.Reason, true);
                    else
                        s_logger.LogWarning("Code: {ErrorCode}, Reason: {ErrorMessage}, Fatal: {FatalError}", error.Code, error.Reason, false);
                })
                .Build();

            s_logger.LogInformation("Kafka consumer subscribing to {Topic}", Topic);
            _consumer.Subscribe(new []{ Topic.Value });

            _creator = new KafkaMessageCreator();
            
            MakeChannels = makeChannels;
            Topic = routingKey;
            NumPartitions = numPartitions;
            ReplicationFactor = replicationFactor;
            TopicFindTimeoutMs = topicFindTimeoutMs;
            
            EnsureTopic();
        }

        /// <summary>
        /// Acknowledges the specified message.
        /// We do not have autocommit on and this stores the message that has just been processed.
        /// We use the header bag to store the partition offset of the message when  reading it from Kafka. This enables us to get hold of it when
        /// we acknowledge the message via Brighter. We store the offset via the consumer, and keep an in-memory list of offsets. If we have hit the
        /// batch size we commit the offsets. if not, we trigger the sweeper, which will commit the offset once the specified time interval has passed if
        /// a batch has not done so.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Acknowledge(Message message)
        {
            if (!message.Header.Bag.TryGetValue(HeaderNames.PARTITION_OFFSET, out var bagData))
                    return;
            
            try
            {
                var topicPartitionOffset = bagData as TopicPartitionOffset;
            
                var offset = new TopicPartitionOffset(topicPartitionOffset.TopicPartition, new Offset(topicPartitionOffset.Offset + 1));

                s_logger.LogInformation("Storing offset {Offset} to topic {Topic} for partition {ChannelName}",
                    new Offset(topicPartitionOffset.Offset + 1).Value, topicPartitionOffset.TopicPartition.Topic,
                    topicPartitionOffset.TopicPartition.Partition.Value);
                _offsetStorage.Add(offset);

                if (_offsetStorage.Count % _maxBatchSize == 0)
                    FlushOffsets();
                else
                    SweepOffsets();

                s_logger.LogInformation("Current Kafka batch count {OffsetCount} and {MaxBatchSize}", _offsetStorage.Count.ToString(), _maxBatchSize.ToString());
            }
            catch (TopicPartitionException tpe)
            {
                var results = tpe.Results.Select(r =>
                    $"Error committing topic {r.Topic} for partition {r.Partition.Value.ToString()} because {r.Error.Reason}");
                var errorString = string.Join(Environment.NewLine, results);
                s_logger.LogDebug("Error committing offsets: {0} {ErrorMessage}", Environment.NewLine, errorString);
            }
        }

       /// <summary>
        /// There is no 'queue' to purge in Kafka, so we treat this as moving past to the offset to tne end of any assigned partitions,
        /// thus skipping over anything that exists at that point.
        /// </summary>
        public void Purge()
        {
            if (!_consumer.Assignment.Any())
                return;

            foreach (var topicPartition in _consumer.Assignment)
            {
                _consumer.Seek(new TopicPartitionOffset(topicPartition, Offset.End));
            }
        }

        /// <summary>
        /// Receives from the specified topic. Used by a <see cref="Channel"/> to provide access to the stream.
        /// We consume the next offset from the stream, and turn it into a Brighter message; we store the offset in the partition into the Brighter message
        /// headers for use in storing and committing offsets. If the stream is EOF or we are not allocated partitions, returns an empty message. 
        /// </summary>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <returns>A Brighter message wrapping the payload from the Kafka stream</returns>
        /// <exception cref="ChannelFailureException">We catch Kafka consumer errors and rethrow as a ChannelFailureException </exception>
        public Message[] Receive(int timeoutInMilliseconds)
        {
            try
            {
                
                LogOffSets();

                s_logger.LogDebug(
                    "Consuming messages from Kafka stream, will wait for {Timeout}", timeoutInMilliseconds);
                var consumeResult = _consumer.Consume(new TimeSpan(0, 0, 0, 0, timeoutInMilliseconds));

                if (consumeResult == null)
                {
                    CheckHasPartitions();
                    
                    s_logger.LogDebug($"No messages available from Kafka stream");
                    return new[] {new Message()};
                }

                if (consumeResult.IsPartitionEOF)
                {
                    s_logger.LogDebug("Consumer {ConsumerMemberId} has reached the end of the partition", _consumer.MemberId);
                    return new[] {new Message()};
                }

                s_logger.LogDebug("Usable message retrieved from Kafka stream: {Request}", consumeResult.Message.Value);
                s_logger.LogDebug("Partition: {ChannelName} Offset: {Offset} Value: {Request}", consumeResult.Partition,
                    consumeResult.Offset, consumeResult.Message.Value);

                return new[] {_creator.CreateMessage(consumeResult)};
            }
            catch (ConsumeException consumeException)
            {
                s_logger.LogError( consumeException,
                    "KafkaMessageConsumer: There was an error listening to topic {Topic} with groupId {ConsumerGroupId} on bootstrap servers: {Servers})",
                    Topic, _consumerConfig.GroupId, _consumerConfig.BootstrapServers);
                throw new ChannelFailureException("Error connecting to Kafka, see inner exception for details", consumeException);

            }
            catch (KafkaException kafkaException)
            {
                s_logger.LogError(kafkaException,
                    "KafkaMessageConsumer: There was an error listening to topic {Topic} with groupId {ConsumerGroupId} on bootstrap servers: {Servers})",
                    Topic, _consumerConfig.GroupId, _consumerConfig.BootstrapServers);
                if (kafkaException.Error.IsFatal) //this can't be recovered and requires a new consumer
                    throw;
                
                throw new ChannelFailureException("Error connecting to Kafka, see inner exception for details", kafkaException);
            }
            catch (Exception exception)
            {
                s_logger.LogError(exception,
                    "KafkaMessageConsumer: There was an error listening to topic {Topic} with groupId {ConsumerGroupId} on bootstrap servers: {Servers})",
                    Topic, _consumerConfig.GroupId, _consumerConfig.BootstrapServers);
                throw;
            }
        }


        /// <summary>
        /// Rejects the specified message. This is just a commit of the offset to move past the record without processing it
        /// </summary>
        /// <param name="message">The message.</param>
        public bool Reject(Message message)
        {
            Acknowledge(message);
            return true;
        }

        /// <summary>
        /// Requeues the specified message. A no-op on Kafka as the stream is immutable
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delayMilliseconds">Number of milliseconds to delay delivery of the message.</param>
        /// <returns>False as no requeue support on Kafka</returns>
        public bool Requeue(Message message, int delayMilliseconds)
        {
            return false;
        }
        
        private void CheckHasPartitions()
        {
            if (_partitions.Count <= 0)
                s_logger.LogDebug("Consumer is not allocated any partitions");
        }


        [Conditional("DEBUG")]
        [DebuggerStepThrough]
        private void LogOffSets()
        {
            try
            {
                var highestReadOffset = new Dictionary<TopicPartition, long>();
            
                var committedOffsets = _consumer.Committed(_partitions, TimeSpan.FromMilliseconds(_readCommittedOffsetsTimeoutMs));
                foreach (var committedOffset in committedOffsets)
                {
                    if (highestReadOffset.TryGetValue(committedOffset.TopicPartition, out long offset))
                    {
                        if (committedOffset.Offset < offset) continue;
                    }
                    highestReadOffset[committedOffset.TopicPartition] = committedOffset.Offset;
                }

                foreach (KeyValuePair<TopicPartition,long> pair in highestReadOffset)
                {
                    var topicPartition = pair.Key;
                    s_logger.LogDebug(
                        "Offset to consume from is: {Offset} on partition: {ChannelName} for topic: {Topic}",
                        pair.Value.ToString(), topicPartition.Partition.Value.ToString(), topicPartition.Topic);
                }
            }
            catch (KafkaException ke)
            {
                //This is only login for debug, so skip errors here
                s_logger.LogDebug("kafka error logging the offsets: {ErrorMessage}", ke.Message);
            }
        }

        /// <summary>
        /// Mainly used diagnostically in tests - how many offsets do we have now?
        /// </summary>
        /// <returns></returns>
        public int StoredOffsets()
        {
            return _offsetStorage.Count;
        }
        
        /// <summary>
        /// We commit a batch size worth at a time; this may be called from the sweeper thread, and we don't want it to
        /// loop endlessly over the offset list as new items are added, which will trigger a commit anyway. So we limit
        /// the trigger to only commit a batch size worth
        /// </summary>
        private void CommitOffsets()
        {
            try
            {
                var listOffsets = new List<TopicPartitionOffset>();
                for (int i = 0; i < _maxBatchSize; i++)
                {
                    bool hasOffsets = _offsetStorage.TryTake(out var offset);
                    if (hasOffsets)
                        listOffsets.Add(offset);
                    else
                        break;

                }

                if (s_logger.IsEnabled(LogLevel.Information))
                {
                    var offsets = listOffsets.Select(tpo =>
                        $"Topic: {tpo.Topic} Partition: {tpo.Partition.Value} Offset: {tpo.Offset.Value}");
                    var offsetAsString = string.Join(Environment.NewLine, offsets);
                    s_logger.LogInformation("Commiting offsets: {0} {Offset}", Environment.NewLine, offsetAsString);
                }

                _consumer.Commit(listOffsets);
            }
            finally
            {
                _flushToken.Release(1);
            }
        }

        //Called during a revoke, we are passed the partitions that we are revoking and their last offset and we need to
        //commit anything we have not stored.
        private void CommitOffsetsFor(List<TopicPartitionOffset> revokedPartitions)
        {
            try
            {
                //find the provided set of partitions amongst our stored offsets 
                var partitionOffsets = _offsetStorage.ToArray();
                var revokedOffsetsToCommit =
                    partitionOffsets.Where(tpo =>
                            revokedPartitions.Any(ptc =>
                                ptc.TopicPartition == tpo.TopicPartition 
                                && ptc.Offset.Value != Offset.Unset.Value 
                                && tpo.Offset.Value > ptc.Offset.Value 
                            )
                        )
                        .ToList();
                //determine if we have offsets still to commit
                if (revokedOffsetsToCommit.Any())
                {
                    //commit them
                    LogOffSetCommitRevokedPartitions(revokedOffsetsToCommit);
                    _consumer.Commit(revokedOffsetsToCommit);
                }
            }
            catch (KafkaException error)
            {
                s_logger.LogError(
                    "Error Committing Offsets During Partition Revoke: {Message} Code: {ErrorCode}, Reason: {ErrorMessage}, Fatal: {FatalError}", 
                    error.Message, error.Error.Code, error.Error.Reason, error.Error.IsFatal
                ); 
            }
        }

        [Conditional("DEBUG")]
        [DebuggerStepThrough]
        private void LogOffSetCommitRevokedPartitions(List<TopicPartitionOffset> revokedOffsetsToCommit)
        {
            s_logger.LogDebug("Saving revoked partition offsets: {OffSetCount}", revokedOffsetsToCommit.Count);
            foreach (var offset in revokedOffsetsToCommit)
            {
                s_logger.LogDebug("Saving revoked partition offset: {Offset} on partition: {Partition} for topic: {Topic}",
                    offset.Offset.Value.ToString(), offset.Partition.Value.ToString(), offset.Topic);
            }
        }
        
        //Just flush everything
        private void CommitAllOffsets(DateTime flushTime)
        {
            try
            {
                var listOffsets = new List<TopicPartitionOffset>();
                var currentOffsetsInBag = _offsetStorage.Count;
                for (int i = 0; i < currentOffsetsInBag; i++)
                {
                    bool hasOffsets = _offsetStorage.TryTake(out var offset);
                    if (hasOffsets)
                        listOffsets.Add(offset);
                    else
                        break;

                }

                if (s_logger.IsEnabled(LogLevel.Information))
                {
                    var offsets = listOffsets.Select(tpo =>
                        $"Topic: {tpo.Topic} Partition: {tpo.Partition.Value} Offset: {tpo.Offset.Value}");
                    var offsetAsString = string.Join(Environment.NewLine, offsets);
                    s_logger.LogInformation("Sweeping offsets: {0} {Offset}", Environment.NewLine, offsetAsString);
                }

                _consumer.Commit(listOffsets);
                _lastFlushAt = flushTime;
            }
            finally
            {
                _flushToken.Release(1);
            }
        }

        // The batch size has been exceeded, so flush our offsets
        private void FlushOffsets()
        {
            var now = DateTime.UtcNow;
            if (_flushToken.Wait(TimeSpan.Zero))
            {
                //This is expensive, so use a background thread
                Task.Factory.StartNew(
                    action: _ => CommitOffsets(),
                    state: now,
                    cancellationToken: CancellationToken.None,
                    creationOptions: TaskCreationOptions.DenyChildAttach,
                    scheduler: TaskScheduler.Default);
            }
            else
            {
                s_logger.LogInformation("Skipped committing offsets, as another commit or sweep was running");
            }
        }

        //If it is has been too long since we flushed, flush now to prevent offsets accumulating 
        private void SweepOffsets()
        {
            var now = DateTime.UtcNow;

            if (now - _lastFlushAt < _sweepUncommittedInterval)
            {
                return;
            }
                
            if (_flushToken.Wait(TimeSpan.Zero))
            {
                if (now - _lastFlushAt < _sweepUncommittedInterval)
                {
                    _flushToken.Release(1);
                    return;
                }
                
                //This is expensive, so use a background thread
                Task.Factory.StartNew(
                    action: state => CommitAllOffsets((DateTime)state),
                    state: now,
                    cancellationToken: CancellationToken.None,
                    creationOptions: TaskCreationOptions.DenyChildAttach,
                    scheduler: TaskScheduler.Default);
            }
            else
            {
                s_logger.LogInformation("Skipped sweeping offsets, as another commit or sweep was running");
            }
        }

        private void Close()
        {
            try
            {
                _consumer.Commit();
                
                var committedOffsets = _consumer.Committed(_partitions, TimeSpan.FromMilliseconds(_readCommittedOffsetsTimeoutMs));
                foreach (var committedOffset in committedOffsets)
                    s_logger.LogInformation("Committed offset: {Offset} on partition: {ChannelName} for topic: {Topic}", committedOffset.Offset.Value.ToString(), committedOffset.Partition.Value.ToString(), committedOffset.Topic);

            }
            catch (Exception ex)
            {
                //this may happen if the offset is already committed
                s_logger.LogDebug("Error committing the current offset to Kafka before closing: {ErrorMessage}", ex.Message);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            Close();

            if (!_disposedValue)
            {
                if (disposing)
                {
                    _consumer.Dispose();
                    _consumer = null;
                }

                _disposedValue = true;
            }
        }


        ~KafkaMessageConsumer()
        {
           Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

   }
}
