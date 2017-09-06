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
using System.Text;
using Paramore.Brighter.MessagingGateway.Kafka.Logging;
using Confluent.Kafka;
using Confluent.Kafka.Serialization;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    class KafkaMessageConsumer : IAmAMessageConsumer
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<KafkaMessageConsumer>);
        private Consumer<Null, string> _consumer;
        private bool _disposedValue = false; 

        public KafkaMessageConsumer(string topic, KafkaMessagingGatewayConfiguration config)
        {
            _consumer = new Consumer<Null, string>(config.ToConfig(), null, new StringDeserializer(Encoding.UTF8));
            _consumer.Assign(new List<TopicPartitionOffset> { new TopicPartitionOffset(topic, 0, 0) });
        }

        public void Acknowledge(Message message)
        {
            //TODO: Implement the ack logic for 
            //kafka
            //throw new NotImplementedException();
        }

        public void Purge()
        {
            throw new NotImplementedException();
        }

        public Message Receive(int timeoutInMilliseconds)
        {
            if (!_consumer.Consume(out Message<Null, string> kafkaMsg, timeoutInMilliseconds))
                return new Message();
            var messageHeader = new MessageHeader(Guid.NewGuid(), kafkaMsg.Topic, MessageType.MT_EVENT); 
            var messageBody = new MessageBody(kafkaMsg.Value);
            return new Message(messageHeader, messageBody); 
        }

        public void Reject(Message message, bool requeue)
        {
            throw new NotImplementedException();
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
