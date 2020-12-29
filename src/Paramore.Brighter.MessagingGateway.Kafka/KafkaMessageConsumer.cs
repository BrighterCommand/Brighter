﻿#region Licence
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
    internal class KafkaMessageConsumer : IAmAMessageConsumer
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<KafkaMessageConsumer>);
        private IConsumer<string, string> _consumer;
        private KafkaMessageCreator _creator;
        private readonly string _topic;
        private readonly ConsumerConfig _consumerConfig;
        private bool _autoCommitEnabled = false;
        private bool _disposedValue = false;

        public KafkaMessageConsumer(string groupId, string topic, 
            KafkaMessagingGatewayConfiguration globalConfiguration, 
            KafkaMessagingConsumerConfiguration consumerConfiguration)
        {
            _topic = topic;
            _autoCommitEnabled = consumerConfiguration.EnableAutoCommit;
            _consumerConfig = new ConsumerConfig()
            {
                GroupId = groupId,
                ClientId = globalConfiguration.Name,
                BootstrapServers = string.Join(",",globalConfiguration.BootStrapServers),
                MaxInFlight = globalConfiguration.MaxInFlightRequestsPerConnection,
                SessionTimeoutMs = 6000,
                EnablePartitionEof = true,

                /*/
                 * By default, we always call acknowledge after processing a handler and commit then.
                 * This has the potential to cause a lot of traffic for the Kafka cluster as every commit is a new message on the consumer_offsets topic.  
                 * To lower the load, you can enable AutoCommit and the AutoCommitIntervalMs.  The downside being that if the consumer dies, you may process a message more than once when a new consumer resumes reading a partition.
                 /*/

                AutoCommitIntervalMs = consumerConfiguration.AutoCommitIntervalMs, 
                EnableAutoCommit = consumerConfiguration.EnableAutoCommit,
                AllowAutoCreateTopics = true,
                AutoOffsetReset = consumerConfiguration.OffsetDefault 
            };


            _consumer = new ConsumerBuilder<string, string>(_consumerConfig)
                .SetPartitionsAssignedHandler((consumer, list) =>
                {
                    _logger.Value.InfoFormat($"Assigned partitions: [{string.Join(", ", list)}], member id: {consumer.MemberId}");
                })
                .SetPartitionsRevokedHandler((consumer, list) =>
                {
                    _logger.Value.InfoFormat($"Revoked partitions: [{string.Join(", ", list)}], member id: {consumer.MemberId}");
                })
                .SetErrorHandler((consumer, error) =>
                {
                    if(error.IsBrokerError)
                        _logger.Value.Error($"BrokerError: Member id: {consumer.MemberId}, error: {error}");
                    else
                        _logger.Value.Error($"ConsumeError: Member Id: {consumer.MemberId}, error: {error}");
                })
                .Build();
            
            _logger.Value.InfoFormat($"Kakfa consumer subscribing to {_topic}");

            _consumer.Subscribe(new []{ _topic });

            _creator = new KafkaMessageCreator();
        }


        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Acknowledge(Message message)
        {
            if (!message.Header.Bag.TryGetValue("TopicPartitionOffset", out var bagData))
                return;
            var topicPartitionOffset = bagData as TopicPartitionOffset;
            _logger.Value.DebugFormat($"Commiting message {topicPartitionOffset} as read");
            _consumer.Commit(new[] {topicPartitionOffset});
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
                _logger.Value.Debug($"Partition: {consumeResult.Partition} Offset: {consumeResult.Offset} Vallue: {consumeResult.Message.Value}");

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
        /// Rejects the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="requeue">if set to <c>true</c> [requeue].</param>
         public void Reject(Message message, bool requeue)
        {
            if (!requeue && !_autoCommitEnabled)
                Acknowledge(message);
        }

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        public void Requeue(Message message)
        {
        }

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delayMilliseconds">Number of milliseconds to delay delivery of the message.</param>
        public void Requeue(Message message, int delayMilliseconds)
        {
            Task.Delay(delayMilliseconds).Wait();
            Requeue(message);
        }
        
        protected virtual void Dispose(bool disposing)
        {
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
