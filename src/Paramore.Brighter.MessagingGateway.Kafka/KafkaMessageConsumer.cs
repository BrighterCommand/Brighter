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
        private DateTime _lastFlushAt = DateTime.UtcNow;
        private readonly TimeSpan _sweepUncommittedInterval;
        private readonly object _flushLock = new object();
        private bool _disposedValue;

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

            _consumer = new ConsumerBuilder<string, string>(_consumerConfig)
                .SetPartitionsAssignedHandler((consumer, list) =>
                {
                    var partitions = list.Select(p => $"{p.Topic} : {p.Partition.Value}");
                    
                    _logger.Value.InfoFormat("Parition Added {0}", String.Join(",", partitions));
                    
                    _partitions.AddRange(list);
                })
                .SetPartitionsRevokedHandler((consumer, list) =>
                {
                    _consumer.Commit(list);
                    var revokedPartitions = list.Select(tpo => $"{tpo.Topic} : {tpo.Partition}").ToList();
                    
                    _logger.Value.InfoFormat("Partitions for consumer revoked {0}", string.Join(",", revokedPartitions));
                    
                    _partitions = _partitions.Where(tp => list.All(tpo => tpo.TopicPartition != tp)).ToList();
                })
                .SetPartitionsLostHandler((consumer, list) =>
                {
                    var lostPartitions = list.Select(tpo => $"{tpo.Topic} : {tpo.Partition}").ToList();
                    
                    _logger.Value.InfoFormat("Partitions for consumer lost {0}", string.Join(",", lostPartitions));
                    
                    _partitions = _partitions.Where(tp => list.All(tpo => tpo.TopicPartition != tp)).ToList();
                })
                .SetErrorHandler((consumer, error) =>
                {
                    _logger.Value.Error($"Code: {error.Code}, Reason: {error.Reason}, Fatal: {error.IsFatal}");
                })
                .Build();
            
            _logger.Value.InfoFormat($"Kakfa consumer subscribing to {Topic}");
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
            
                _logger.Value.InfoFormat($"Storing offset {new Offset(topicPartitionOffset.Offset + 1).Value} to topic {topicPartitionOffset.TopicPartition.Topic} for partition {topicPartitionOffset.TopicPartition.Partition.Value}");
                _consumer.StoreOffset(offset);
                _offsetStorage.Add(offset);

                if (_offsetStorage.Count % _maxBatchSize == 0)
                    FlushOffsets();
                else
                    SweepOffsets();

                _logger.Value.InfoFormat($"Current Kafka batch count {_offsetStorage.Count.ToString()} and {_maxBatchSize.ToString()}");
            }
            catch (TopicPartitionException tpe)
            {
                var results = tpe.Results.Select(r =>
                    $"Error committing topic {r.Topic} for partition {r.Partition.Value.ToString()} because {r.Error.Reason}");
                var errorString = string.Join(Environment.NewLine, results);
                _logger.Value.Debug($"Error committing offsets: {Environment.NewLine} {errorString}");
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

                _logger.Value.DebugFormat(
                    $"Consuming messages from Kafka stream, will wait for {timeoutInMilliseconds}");
                var consumeResult = _consumer.Consume(new TimeSpan(0, 0, 0, 0, timeoutInMilliseconds));

                if (consumeResult == null)
                {
                    CheckHasPartitions();
                    
                    _logger.Value.DebugFormat($"No messages available from Kafka stream");
                    return new Message[] {new Message()};
                }

                if (consumeResult.IsPartitionEOF)
                {
                    _logger.Value.Debug($"Consumer {_consumer.MemberId} has reached the end of the partition");
                    return new Message[] {new Message()};
                }

                _logger.Value.DebugFormat($"Usable message retrieved from Kafka stream: {consumeResult.Message.Value}");
                _logger.Value.Debug($"Partition: {consumeResult.Partition} Offset: {consumeResult.Offset} Value: {consumeResult.Message.Value}");

                return new[] {_creator.CreateMessage(consumeResult)};
            }
            catch (ConsumeException consumeException)
            {
                _logger.Value.ErrorException(
                    $"KafkaMessageConsumer: There was an error listening to topic {Topic} with groupId {_consumerConfig.GroupId} on bootstrap servers: {_consumerConfig.BootstrapServers})",
                    consumeException);
                throw new ChannelFailureException("Error connecting to Kafka, see inner exception for details", consumeException);

            }
            catch (KafkaException kafkaException)
            {
                _logger.Value.ErrorException(
                    $"KafkaMessageConsumer: There was an error listening to topic {Topic} with groupId {_consumerConfig.GroupId} on bootstrap servers: {_consumerConfig.BootstrapServers})",
                    kafkaException);
                if (kafkaException.Error.IsFatal) //this can't be recovered and requires a new consumer
                    throw;
                
                throw new ChannelFailureException("Error connecting to Kafka, see inner exception for details", kafkaException);
            }
            catch (Exception exception)
            {
                _logger.Value.ErrorException(
                    $"KafkaMessageConsumer: There was an error listening to topic {Topic} with groupId {_consumerConfig.GroupId} on bootstrap servers: {_consumerConfig.BootstrapServers})", 
                    exception);
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
        /// Requeues the specified message. A no-op on Kafka as the stream is immutable
        /// </summary>
        /// <param name="message"></param>
        public void Requeue(Message message)
        {
        }

        /// <summary>
        /// Requeues the specified message. A no-op on Kafka as the stream is immutable
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delayMilliseconds">Number of milliseconds to delay delivery of the message.</param>
        public void Requeue(Message message, int delayMilliseconds)
        {
            Task.Delay(delayMilliseconds).Wait();
            Requeue(message);
        }
        
        private bool CheckHasPartitions()
        {
            if (_partitions.Count <= 0)
            {
                _logger.Value.Debug("Consumer is not allocated any partitions");
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
                    var message = $"Offset to consume from is: {pair.Value.ToString()} on partition: {topicPartition.Partition.Value.ToString()} for topic: {topicPartition.Topic}";
                    _logger.Value.Debug(message);
                }
            }
            catch (KafkaException ke)
            {
                //This is only loggin for debug, so skip errors here
                _logger.Value.Debug($"kafka error logging the offsets: {ke.Message}");
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
           if (_logger.Value.IsDebugEnabled())
           {
               var offsets = _offsetStorage.Select(tpo => $"Topic: {tpo.Topic} Partition: {tpo.Partition.Value} Offset: {tpo.Offset.Value}");
               var offsetAsString = string.Join(Environment.NewLine, offsets);
               _logger.Value.DebugFormat($"Commiting all offsets: {Environment.NewLine} {offsetAsString}");
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
                    _logger.Value.Info(
                        $"Committed offset: {committedOffset.Offset.Value.ToString()} on partition: {committedOffset.Partition.Value.ToString()} for topic: {committedOffset.Topic}");

            }
            catch (Exception ex)
            {
                //this may happen if the offset is already committed
                _logger.Value.Debug($"Error committing the current offset to Kakfa before closing: {ex.Message}");
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
