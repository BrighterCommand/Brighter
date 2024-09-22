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
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Confluent.Kafka;
using FluentAssertions;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;
using SaslMechanism = Paramore.Brighter.MessagingGateway.Kafka.SaslMechanism;
using SecurityProtocol = Paramore.Brighter.MessagingGateway.Kafka.SecurityProtocol;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway
{
    [Trait("Category", "Kafka")]
    [Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
     public class KafkaProducerAssumeTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _queueName = Guid.NewGuid().ToString(); 
        private readonly string _topic = Guid.NewGuid().ToString();
        private readonly IAmAProducerRegistry _producerRegistry;
        private readonly string _partitionKey = Guid.NewGuid().ToString();

        public KafkaProducerAssumeTests(ITestOutputHelper output)
        {
            _output = output;
            _producerRegistry = new KafkaProducerRegistryFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Producer Send Test", 
                    BootStrapServers = new[] {"localhost:9092"}
                },
                new KafkaPublication[] {new KafkaPublication
                {
                    Topic = new RoutingKey(_topic),
                    NumPartitions = 1,
                    ReplicationFactor = 1,
                    //These timeouts support running on a container using the same host as the tests, 
                    //your production values ought to be lower
                    MessageTimeoutMs = 2000,
                    RequestTimeoutMs = 2000,
                    MakeChannels = OnMissingChannel.Create
                }}).Create(); 
            
        }

        //[Fact(Skip = "Does not fail on docker container as has topic creation set to true")]
        [Fact]
        public void When_a_consumer_declares_topics()
        {
            var routingKey = new RoutingKey(_topic);
            
            var message = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND)
                {
                    PartitionKey = _partitionKey
                },
                new MessageBody($"test content [{_queueName}]")
            );
            
            bool messagePublished = false;
            var producer = _producerRegistry.LookupBy(routingKey);
            var producerConfirm = producer as ISupportPublishConfirmation;
            producerConfirm.OnMessagePublished += delegate(bool success, string id)
            {
                if (success) messagePublished = true;
            };
            
            ((IAmAMessageProducerSync)producer).Send(message);

            //Give this a chance to succeed - will fail
            Task.Delay(5000);

            messagePublished.Should().BeFalse();
        }

        public void Dispose()
        {
            _producerRegistry?.Dispose();
        }
    }
}
