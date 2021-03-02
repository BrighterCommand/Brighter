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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    public class KafkaMessageConsumer : IAmAMessageConsumer
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<KafkaMessageConsumer>);
        private IConsumer<string, string> _consumer;
        private readonly KafkaMessageCreator _creator;
        private readonly RoutingKey _topic;
        private readonly ConsumerConfig _consumerConfig;
        private List<TopicPartition> _partitions = new List<TopicPartition>();
        private readonly List<TopicPartitionOffset> _offsets = new List<TopicPartitionOffset>();
        private bool _disposedValue;
        private readonly long _maxBatchSize;
        private readonly int _readCommittedOffsetsTimeoutMs;

        public KafkaMessageConsumer(
            KafkaMessagingGatewayConfiguration configuration,
            RoutingKey routingKey,
            string groupId,
            AutoOffsetReset offsetDefault = AutoOffsetReset.Earliest,
            int sessionTimeoutMs = 10000,
            int maxPollIntervalMs = 10000,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            long commitBatchSize = 10,
            int readCommittedOffsetsTimeoutMs = 5000,
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
            
            _topic = routingKey;
            
            _consumerConfig = new ConsumerConfig(
                new ClientConfig
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
            })
            {
                GroupId = groupId,
                ClientId = configuration.Name,
                AutoOffsetReset = offsetDefault,
                BootstrapServers = string.Join(",", configuration.BootStrapServers),
                SessionTimeoutMs = sessionTimeoutMs,
                MaxPollIntervalMs = maxPollIntervalMs,
                EnablePartitionEof = true,
                AllowAutoCreateTopics = makeChannels == OnMissingChannel.Create,
                IsolationLevel = isolationLevel,
                //We commit the last offset for acknowledged requests when a batch of records has been processed. 
                EnableAutoOffsetStore = false,
                EnableAutoCommit = false,
            };

            _maxBatchSize = commitBatchSize;
            _readCommittedOffsetsTimeoutMs = readCommittedOffsetsTimeoutMs;

            _consumer = new ConsumerBuilder<string, string>(_consumerConfig)
                .SetPartitionsAssignedHandler((consumer, list) => _partitions.AddRange(list))
                .SetPartitionsRevokedHandler((consumer, list) =>
                {
                    _consumer.Commit(list);
                    var revokedPartitions = list.Select(tpo => tpo.Partition).ToList();
                    _partitions = _partitions.Where(tp => !revokedPartitions.Contains(tp.Partition)).ToList();
                })
                .Build();
            
            _logger.Value.InfoFormat($"Kakfa consumer subscribing to {_topic}");
            _consumer.Subscribe(new []{ _topic.Value });

            _creator = new KafkaMessageCreator();
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
                _offsets.Add(offset);
             
                if (_offsets.Count % _maxBatchSize == 0)
                {
                    if (_logger.Value.IsInfoEnabled())
                    {
                        var offsets = _offsets.Select(tpo => $"Topic: {tpo.Topic} Partition: {tpo.Partition.Value} Offset: {tpo.Offset.Value}");
                        var offsetAsString = string.Join(Environment.NewLine, offsets);
                        _logger.Value.InfoFormat($"Commiting all offsets: {Environment.NewLine} {offsetAsString}");
                    }

                    _consumer.Commit(_offsets);
                    _offsets.Clear();
                }

                _logger.Value.InfoFormat($"Current Kafka batch count {_offsets.Count.ToString()} and {_maxBatchSize.ToString()}");
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
                    _logger.Value.InfoFormat($"No messages available from Kafka stream");
                    return new Message[] {new Message()};
                }

                if (consumeResult.IsPartitionEOF)
                {
                    _logger.Value.Info($"Consumer {_consumer.MemberId} has reached the end of the partition");
                    return new Message[] {new Message()};
                }

                _logger.Value.DebugFormat($"Usable message retrieved from Kafka stream: {consumeResult.Message.Value}");
                _logger.Value.Debug($"Partition: {consumeResult.Partition} Offset: {consumeResult.Offset} Value: {consumeResult.Message.Value}");

                return new []{_creator.CreateMessage(consumeResult)};
            }
            catch (ConsumeException consumeException)
            {
                 _logger.Value.ErrorException(
                     "KafkaMessageConsumer: There was an error listening to topic {0} with groupId {1) on bootstrap servers: {2) and consumer record {3}", 
                     consumeException, 
                     _topic, 
                     _consumerConfig.GroupId, 
                     _consumerConfig.BootstrapServers,
                     consumeException.ConsumerRecord.ToString());
                 throw new ChannelFailureException("Error connecting to Kafka, see inner exception for details", consumeException);
                 
            }
            catch (Exception exception)
            {
                _logger.Value.ErrorException(
                    "KafkaMessageConsumer: There was an error listening to topic {0} with groupId {1) on bootstrap servers: {2)", 
                    exception, 
                    _topic, 
                    _consumerConfig.GroupId, 
                    _consumerConfig.BootstrapServers);
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
                    _logger.Value.Info(message);
                }
            }
            catch (KafkaException ke)
            {
                //This is only loggin for debug, so skip errors here
                _logger.Value.Debug($"kafka error logging the offsets: {ke.Message}");
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
