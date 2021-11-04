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
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    /// <inheritdoc />
    /// <summary>
    /// Class KafkaMessageConsumer is an implementation of <see cref="IAmAMessageConsumer"/>
    /// and provides the facilities to consume messages from a Kafka broker for a topic
    /// in a consumer group.
    /// </summary>
    public class KafkaMessageConsumer : KafkaMessagingGateway, IAmAMessageConsumer
    {
        private IConsumer<string, string> _consumer;
        private readonly KafkaMessageCreator _creator;
        private readonly ConsumerConfig _consumerConfig;
        private List<TopicPartition> _partitions = new List<TopicPartition>();
        private readonly ConcurrentBag<TopicPartitionOffset> _offsetStorage = new ConcurrentBag<TopicPartitionOffset>();
        private readonly long _maxBatchSize;
        private readonly int _readCommittedOffsetsTimeoutMs;
        private readonly RoutingKey _deferQueueTopic;
        private DateTime _lastFlushAt = DateTime.UtcNow;
        private readonly TimeSpan _sweepUncommittedInterval;
        private readonly object _flushLock = new object();
        private bool _disposedValue;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="routingKey"></param>
        /// <param name="groupId"></param>
        /// <param name="offsetDefault"></param>
        /// <param name="sessionTimeoutMs"></param>
        /// <param name="maxPollIntervalMs"></param>
        /// <param name="isolationLevel"></param>
        /// <param name="commitBatchSize"></param>
        /// <param name="sweepUncommittedOffsetsIntervalMs"></param>
        /// <param name="readCommittedOffsetsTimeoutMs"></param>
        /// <param name="numPartitions"></param>
        /// <param name="replicationFactor"></param>
        /// <param name="topicFindTimeoutMs"></param>
        /// <param name="deferQueueTopic"></param>
        /// <param name="makeChannels"></param>
        /// <exception cref="ConfigurationException"></exception>
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
            short replicationFactor = 1,
            int topicFindTimeoutMs = 10000,
            RoutingKey deferQueueTopic = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create
            )
        {
            if (configuration is null)
            {
                throw new ConfigurationException("You must set a KafkaMessaginGatewayConfiguration to connect to a broker");
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
            _consumerConfig = new ConsumerConfig(_clientConfig)
            {
                GroupId = groupId,
                ClientId = configuration.Name,
                AutoOffsetReset = offsetDefault,
                BootstrapServers = string.Join(",", configuration.BootStrapServers),
                SessionTimeoutMs = sessionTimeoutMs,
                MaxPollIntervalMs = maxPollIntervalMs,
                EnablePartitionEof = true,
                AllowAutoCreateTopics = false, //We will do this explicit always so as to allow us to set parameters for the topic
                IsolationLevel = isolationLevel,
                //We commit the last offset for acknowledged requests when a batch of records has been processed. 
                EnableAutoOffsetStore = false,
                EnableAutoCommit = false,
                // https://www.confluent.io/blog/cooperative-rebalancing-in-kafka-streams-consumer-ksqldb/
                PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky,
            };

            _maxBatchSize = commitBatchSize;
            _sweepUncommittedInterval = TimeSpan.FromMilliseconds(sweepUncommittedOffsetsIntervalMs);
            _readCommittedOffsetsTimeoutMs = readCommittedOffsetsTimeoutMs;
            _deferQueueTopic = deferQueueTopic;

            _consumer = new ConsumerBuilder<string, string>(_consumerConfig)
                .SetPartitionsAssignedHandler((consumer, list) =>
                {
                    var partitions = list.Select(p => $"{p.Topic} : {p.Partition.Value}");
                    
                    s_logger.LogInformation("Partition Added {Channels}", String.Join(",", partitions));
                    
                    _partitions.AddRange(list);
                })
                .SetPartitionsRevokedHandler((consumer, list) =>
                {
                    _consumer.Commit(list);
                    var revokedPartitions = list.Select(tpo => $"{tpo.Topic} : {tpo.Partition}").ToList();
                    
                    s_logger.LogInformation("Partitions for consumer revoked {Channels}", string.Join(",", revokedPartitions));
                    
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
                    s_logger.LogError("Code: {ErrorCode}, Reason: {ErrorMessage}, Fatal: {FatalError}", error.Code,
                        error.Reason, error.IsFatal);
                })
                .Build();

            s_logger.LogInformation("Kakfa consumer subscribing to {Topic}", Topic);
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
        /// If we have autocommit on, this is essentially a no-op as the offset will be auto-committed via the client
        /// If we do not have autocommit on (the default) then this commits the message that has just been processed.
        /// If you do use autocommit, be aware that you will need to cope with duplicates, perhaps via an Inbox
        /// When we read a message, we store the offset of the message on the partition in the header. This enables us
        /// to commit the message successfully after processing
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
                _consumer.StoreOffset(offset);
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
        /// Rejects the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="requeue">if set to <c>true</c> [requeue].</param>
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
        /// Receives the specified queue name.
        /// An abstraction over a third-party messaging library. Used to read messages from the broker and to acknowledge the processing of those messages or requeue them.
        /// Used by a <see cref="Channel"/> to provide access to a third-party message queue.
        /// </summary>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <returns>Message.</returns>
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
                    return new Message[] {new Message()};
                }

                if (consumeResult.IsPartitionEOF)
                {
                    s_logger.LogDebug("Consumer {ConsumerMemberId} has reached the end of the partition", _consumer.MemberId);
                    return new Message[] {new Message()};
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
        /// on Kafka, as we can't requeue or delete from the queue
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="requeue">if set to <c>true</c> [requeue].</param>
        public void Reject(Message message)
        {
            Acknowledge(message);
        }

        /// <summary>
        /// Requeues the specified message.
        /// We requeue if the  
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delayMilliseconds">Number of milliseconds to delay delivery of the message.</param>
        /// <returns>False as no requeue support on Kafka</returns>
        public bool Requeue(Message message, int delayMilliseconds)
        {
            return false;
        }
        
        private bool CheckHasPartitions()
        {
            if (_partitions.Count <= 0)
            {
                s_logger.LogDebug("Consumer is not allocated any partitions");
                return false;
            }

            return true;
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
                //This is only loggin for debug, so skip errors here
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
        private void CommitOffsets(DateTime flushTime)
        {
           if (s_logger.IsEnabled(LogLevel.Debug))
           {
               var offsets = _offsetStorage.Select(tpo => $"Topic: {tpo.Topic} Partition: {tpo.Partition.Value} Offset: {tpo.Offset.Value}");
               var offsetAsString = string.Join(Environment.NewLine, offsets);
               s_logger.LogDebug("Commiting all offsets: {0} {Offset}", Environment.NewLine, offsetAsString);
           }
            
           var listOffsets = new List<TopicPartitionOffset>();
           for (int i = 0; i < _maxBatchSize; i++)
           {
               bool hasOffsets = _offsetStorage.TryTake(out var offset);
               if (hasOffsets)
                   listOffsets.Add(offset);
               else
                   break;

           }
           _consumer.Commit(listOffsets);
           _lastFlushAt = flushTime;
           Monitor.Exit(_flushLock);
        }

        // The batch size has been exceeded, so flush our offsets
        private void FlushOffsets()
        {
            var now = DateTime.UtcNow;
            if (Monitor.TryEnter(_flushLock))
            {
                //This is expensive, so use a background thread
                Task.Factory.StartNew(
                    action: state => CommitOffsets((DateTime)state),
                    state: now,
                    cancellationToken: CancellationToken.None,
                    creationOptions: TaskCreationOptions.DenyChildAttach,
                    scheduler: TaskScheduler.Default);
            } 
        }

        //If it is has been too long since we flushed, flush now to prevent offsets accumulating 
        private void SweepOffsets()
        {
            var now = DateTime.UtcNow;

            if (Monitor.TryEnter(_flushLock))
            {

                if (now - _lastFlushAt < _sweepUncommittedInterval)
                    return;

                //This is expensive, so use a background thread
                Task.Factory.StartNew(
                    action: state => CommitOffsets((DateTime)state),
                    state: now,
                    cancellationToken: CancellationToken.None,
                    creationOptions: TaskCreationOptions.DenyChildAttach,
                    scheduler: TaskScheduler.Default);
            }
        }

        private void Close()
        {
            try
            {
                _consumer.Commit();
                
                var committedOffsets = _consumer.Committed(_partitions, TimeSpan.FromMilliseconds(_readCommittedOffsetsTimeoutMs));
                foreach (var committedOffset in committedOffsets)
                    s_logger.LogInformation("Committed offset: {Offset)} on partition: {ChannelName} for topic: {Topic}", committedOffset.Offset.Value.ToString(), committedOffset.Partition.Value.ToString(), committedOffset.Topic);

            }
            catch (Exception ex)
            {
                //this may happen if the offset is already committed
                s_logger.LogDebug("Error committing the current offset to Kakfa before closing: {ErrorMessage}", ex.Message);
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
