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
using System.Threading.Tasks;
using System.Text;
using Confluent.Kafka;
using Confluent.Kafka.Serialization;
using Paramore.Brighter.MessagingGateway.Kafka.Logging;
using System.Linq;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    internal class KafkaMessageProducer : IAmAMessageProducer, IAmAMessageProducerAsync
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<KafkaMessageProducer>);
        private Producer<Null, string> _producer;
        private bool _disposedValue = false; 

        public KafkaMessageProducer(KafkaMessagingGatewayConfiguration globalConfiguration, 
            KafkaMessagingProducerConfiguration producerConfiguration)
        {
            var serialiser = new StringSerializer(Encoding.UTF8);
            var config = globalConfiguration.ToConfig();
            config = config.Concat(producerConfiguration.ToConfig());
            _producer = new Producer<Null, string>(config, null, serialiser);
        }

        public void Send(Message message)
        {
            SendAsync(message).Wait();
        }

        
        public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            //No delay support implemented
            Send(message);
        }
        
        
        public Task SendAsync(Message message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            return _producer.ProduceAsync(message.Header.Topic, null, message.Body.Value);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _producer.Dispose();
                    _producer = null;
                }

                _disposedValue = true;
            }
        }

        ~KafkaMessageProducer()
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
