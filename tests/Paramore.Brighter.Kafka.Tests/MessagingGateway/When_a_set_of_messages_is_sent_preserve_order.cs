﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using FluentAssertions;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway
{
    [Trait("Category", "Kafka")]
    [Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
     public class KafkaMessageConsumerPreservesOrder : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _queueName = Guid.NewGuid().ToString();
        private readonly string _topic = Guid.NewGuid().ToString();
        private readonly IAmAMessageProducer _producer;
        private readonly string _partitionKey = Guid.NewGuid().ToString();
        private readonly string _kafkaGroupId = Guid.NewGuid().ToString();


        public KafkaMessageConsumerPreservesOrder (ITestOutputHelper output)
        {
            _output = output;
            _producer = new KafkaMessageProducerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Producer Send Test", 
                    BootStrapServers = new[] {"localhost:9092"},
                    
                },
                new KafkaPublication()
                {
                    Topic = new RoutingKey(_topic),
                    NumPartitions = 1,
                    ReplicationFactor = 1,
                    //These timeouts support running on a container using the same host as the tests, 
                    //your production values ought to be lower
                    MessageTimeoutMs = 2000,
                    RequestTimeoutMs = 2000,
                    MakeChannels = OnMissingChannel.Create
                }
                ).Create();
        }

        [Fact]
        public void When_a_message_is_sent_keep_order()
        {
            IAmAMessageConsumer consumer = null;
            try
            {
                //Send a sequence of messages to Kafka
                var msgId = SendMessage();
                var msgId2 = SendMessage();
                var msgId3 = SendMessage();
                var msgId4 = SendMessage();
                  
                consumer = CreateConsumer();
                
                //Now read those messages in order
                
                var firstMessage = ConsumeMessages(consumer);
                var message = firstMessage.First();
                message.Id.Should().Be(msgId);
                consumer.Acknowledge(message);

                var secondMessage = ConsumeMessages(consumer);
                message = secondMessage.First();
                message.Id.Should().Be(msgId2);
                consumer.Acknowledge(message);               
                
                var thirdMessages = ConsumeMessages(consumer);
                message = thirdMessages .First();
                message.Id.Should().Be(msgId3);
                consumer.Acknowledge(message);               
                
                var fourthMessage = ConsumeMessages(consumer);
                message = fourthMessage .First();
                message.Id.Should().Be(msgId4);
                consumer.Acknowledge(message);               
 
            }
            finally
            {
                consumer?.Dispose();
            }
        }

        private Guid SendMessage()
        {
            var messageId = Guid.NewGuid();

            _producer.Send(new Message(
                new MessageHeader(messageId, _topic, MessageType.MT_COMMAND)
                {
                    PartitionKey = _partitionKey
                },
                new MessageBody($"test content [{_queueName}]")));

            return messageId;
        }

        private IEnumerable<Message> ConsumeMessages(IAmAMessageConsumer consumer)
        {
            var messages = new Message[0];
            int maxTries = 0;
            do
            {
                try
                {
                    maxTries++;
                    Task.Delay(500).Wait(); //Let topic propogate in the broker
                    messages = consumer.Receive(1000);

                    if (messages[0].Header.MessageType != MessageType.MT_NONE)
                        break;
                }
                catch (ChannelFailureException cfx)
                {
                    //Lots of reasons to be here as Kafka propogates a topic, or the test cluster is still initializing
                    _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                }
            } while (maxTries <= 3);

            return messages;
        }

        private IAmAMessageConsumer CreateConsumer()
        {
            return new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer Test",
                    BootStrapServers = new[] { "localhost:9092" }
                })
                .Create(new KafkaSubscription<MyCommand>(
                        name: new SubscriptionName("Paramore.Brighter.Tests"),
                        channelName: new ChannelName(_queueName),
                        routingKey: new RoutingKey(_topic),
                        groupId: _kafkaGroupId,
                        offsetDefault: AutoOffsetReset.Earliest,
                        commitBatchSize:1,
                        numOfPartitions: 1,
                        replicationFactor: 1,
                        makeChannels: OnMissingChannel.Create
                    ));
        }

        public void Dispose()
        {
            _producer?.Dispose();
        }
    }
}
