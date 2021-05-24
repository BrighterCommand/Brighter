﻿#region Licence
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
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway
{
    [Trait("Category", "Kafka")]
    [Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
    public class KafkaConsumerDeclareTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _queueName = Guid.NewGuid().ToString(); 
        private readonly string _topic = Guid.NewGuid().ToString();
        private readonly IAmAMessageProducer _producer;
        private readonly IAmAMessageConsumer _consumer;
        private readonly string _partitionKey = Guid.NewGuid().ToString();

        public KafkaConsumerDeclareTests (ITestOutputHelper output)
        {
            const string groupId = "Kafka Message Producer Send Test";
            _output = output;
            _producer = new KafkaMessageProducerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Producer Send Test",
                    BootStrapServers = new[] {"localhost:9092"}
                },
                new KafkaPublication
                {
                    Topic = new RoutingKey(_topic),
                    NumPartitions = 1,
                    ReplicationFactor = 1,
                    //These timeouts support running on a container using the same host as the tests, 
                    //your production values ought to be lower
                    MessageTimeoutMs = 10000,
                    RequestTimeoutMs = 10000,
                    MakeChannels = OnMissingChannel.Assume //This will not make the topic
               }).Create(); 
            
            //This should force creation of the topic - will fail if no topic creation code
            _consumer = new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Producer Send Test",
                    BootStrapServers = new[] {"localhost:9092"}
                })
                .Create(new KafkaSubscription<MyCommand>(
                     channelName: new ChannelName(_queueName), 
                     routingKey: new RoutingKey(_topic),
                     groupId: groupId,
                     numOfPartitions: 1,
                     replicationFactor: 1,
                     makeChannels: OnMissingChannel.Create
                     )
             );
  
        }

        [Fact]
        public void When_a_consumer_declares_topics()
        {
            var message = new Message(
                new MessageHeader(Guid.NewGuid(), _topic, MessageType.MT_COMMAND)
                {
                    PartitionKey = _partitionKey
                },
                new MessageBody($"test content [{_queueName}]"));
            
            //This should fail, if consumer can't create the topic as set to Assume
            _producer.Send(message);

            Message[] messages = new Message[0];
            int maxTries = 0;
            do
            {
                try
                {
                    maxTries++;
                    Task.Delay(500).Wait(); //Let topic propogate in the broker
                    messages = _consumer.Receive(10000);
                    _consumer.Acknowledge(messages[0]);
                    
                    if (messages[0].Header.MessageType != MessageType.MT_NONE)
                        break;
                        
                }
                catch (ChannelFailureException cfx)
                {
                    //Lots of reasons to be here as Kafka propogates a topic, or the test cluster is still initializing
                    _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                }

            } while (maxTries <= 3);

            messages.Length.Should().Be(1);
            messages[0].Header.MessageType.Should().Be(MessageType.MT_COMMAND);
            messages[0].Header.PartitionKey.Should().Be(_partitionKey);
            messages[0].Body.Value.Should().Be(message.Body.Value);
        }

        public void Dispose()
        {
            _producer?.Dispose();
            _consumer?.Dispose();
        }
    }
}
