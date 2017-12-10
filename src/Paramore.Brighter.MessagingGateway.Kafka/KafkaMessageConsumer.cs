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
using System.Linq;
using System.Text;
using Paramore.Brighter.MessagingGateway.Kafka.Logging;
using Confluent.Kafka;
using Confluent.Kafka.Serialization;

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
        private Consumer<Null, string> _consumer;
        private bool _disposedValue = false; 

        public KafkaMessageConsumer(string groupId, string topic, 
            KafkaMessagingGatewayConfiguration globalConfiguration, 
            KafkaMessagingConsumerConfiguration consumerConfiguration)
        {
            var config = globalConfiguration.ToConfig();
            config = config.Concat(consumerConfiguration.ToConfig());
            config = config.Concat(new[] {new KeyValuePair<string, object>("group.id", groupId)});
            _consumer = new Consumer<Null, string>(config, null, new StringDeserializer(Encoding.UTF8));

            _consumer.OnPartitionsAssigned += (_, partitions) => OnPartionsAssigned(partitions);
            _consumer.OnPartitionsRevoked += (_, partitions) => OnPartionsRevoked(partitions);

            if (_logger.Value.IsErrorEnabled())
            {
                _consumer.OnError += (_, error) =>
                    _logger.Value.Error($"Member id: {_consumer.MemberId}, error: {error}");
            }

            _consumer.Subscribe(new []{ topic });
        }

        private void OnPartionsAssigned(List<TopicPartition> partitions)
        {
            _logger.Value.InfoFormat($"Assigned partitions: [{string.Join(", ", partitions)}], member id: {_consumer.MemberId}");
            _consumer.Assign(partitions);
        }

        private void OnPartionsRevoked(List<TopicPartition> partitions)
        {
            _logger.Value.InfoFormat($"Revoked partitions: [{string.Join(", ", partitions)}], member id: {_consumer.MemberId}");
            _consumer.Unassign();
        }

        public void Acknowledge(Message message)
        {
            if (!message.Header.Bag.TryGetValue("TopicPartitionOffset", out var bagData))
                return;
            var topicPartitionOffset = bagData as TopicPartitionOffset;
            var deliveryReport = _consumer.CommitAsync(new[] {topicPartitionOffset}).Result;
        }

        public void Purge()
        {
            if (!_consumer.Assignment.Any())
                return;

            foreach (var topicPartition in _consumer.Assignment)
            {
                _consumer.Seek(new TopicPartitionOffset(topicPartition, Offset.End));
            }
        }

        public Message Receive(int timeoutInMilliseconds)
        {
            if (!_consumer.Consume(out Message<Null, string> kafkaMsg, timeoutInMilliseconds))
                return new Message();
            var messageHeader =
                new MessageHeader(Guid.NewGuid(), kafkaMsg.Topic, MessageType.MT_EVENT)
                {
                    Bag = {["TopicPartitionOffset"] = kafkaMsg.TopicPartitionOffset}
                };
            var messageBody = new MessageBody(kafkaMsg.Value);
            return new Message(messageHeader, messageBody); 
        }

        public void Reject(Message message, bool requeue)
        {
            if (!requeue)
                Acknowledge(message);
        }

        public void Requeue(Message message)
        {
            throw new NotImplementedException();
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
