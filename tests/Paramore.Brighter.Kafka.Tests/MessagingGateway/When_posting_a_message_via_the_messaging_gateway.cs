#region Licence
/* The MIT License (MIT)
Copyright © 2014 Wayne Hunsley <whunsley@gmail.com>

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
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway
{
    [Collection("Kafka")]
    [Trait("Category", "Kafka")]
    public class KafkaMessageProducerSendTests : IDisposable
    {
        private readonly string _queueName = Guid.NewGuid().ToString(); 
        private readonly string _topic = Guid.NewGuid().ToString();
        private IAmAMessageProducer _producer;
        private IAmAMessageConsumer _consumer;


        public KafkaMessageProducerSendTests()
        {
            _producer = new KafkaMessageProducerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Producer Send Test",
                    BootStrapServers = new[] {"localhost:9092"}
                }).Create(); 
            
            _consumer = new KafkaMessageConsumerFactory(
                 new KafkaMessagingGatewayConfiguration
                 {
                     Name = "Kafka Consumer Test",
                     BootStrapServers = new[] { "localhost:9092" }
                 }).Create(new Connection<MyCommand>(
                     channelName: new ChannelName(_queueName), 
                     routingKey: new RoutingKey(_topic)
                     )
             );
  
        }

        [Fact]
        public void When_posting_a_message_via_the_messaging_gateway()
        {
            var message = new Message(
                new MessageHeader(Guid.NewGuid(), _topic, MessageType.MT_COMMAND),
                new MessageBody($"test content [{_queueName}]"));
            _producer.Send(message);

            Message[] messages = new Message[0];
            int maxTries = 0;
            do
            {
                try
                {
                    maxTries++;
                    Task.Delay(500).Wait(); //Let topic propogate in the broker
                    messages = _consumer.Receive(1000);
                }
                catch (ChannelFailureException)
                {
                    //Lots of reasons to be here as Kafka propogates a topic, or the test cluster is still initializing
                }

            } while (messages.Length == 0 && maxTries <= 3);


            messages.Length.Should().Be(1);
            _consumer.Acknowledge(messages[0]);
            messages[0].Body.Value.Should().Be(message.Body.Value);
        }

        public void Dispose()
        {
            _producer?.Dispose();
            _consumer?.Dispose();
        }
    }
}
