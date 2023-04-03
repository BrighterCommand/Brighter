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
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using FluentAssertions;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway
{
    [Trait("Category", "Kafka")]
    [Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
    public class KafkaMessageProducerHeaderBytesSendTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _queueName = Guid.NewGuid().ToString(); 
        private readonly string _topic = Guid.NewGuid().ToString();
        private readonly IAmAProducerRegistry _producerRegistry;
        private readonly IAmAMessageConsumer _consumer;
        private readonly string _partitionKey = Guid.NewGuid().ToString();
        private readonly ISchemaRegistryClient _schemaRegistryClient;
        private readonly ISerializer<MyKafkaCommand> _serializer;
        private readonly IDeserializer<MyKafkaCommand> _deserializer;
        private readonly SerializationContext _serializationContext;


        public KafkaMessageProducerHeaderBytesSendTests (ITestOutputHelper output)
        {
            const string groupId = "Kafka Message Producer Header Bytes Send Test";
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
            
            _consumer = new KafkaMessageConsumerFactory(
                    new KafkaMessagingGatewayConfiguration
                    {
                        Name = "Kafka Consumer Test",
                        BootStrapServers = new[] { "localhost:9092" }
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
            
            var schemaRegistryConfig = new SchemaRegistryConfig { Url = "http://localhost:8081"};
            _schemaRegistryClient = new CachedSchemaRegistryClient(schemaRegistryConfig);
  
            _serializer = new JsonSerializer<MyKafkaCommand>(_schemaRegistryClient, ConfluentJsonSerializationConfig.SerdesJsonSerializerConfig(), ConfluentJsonSerializationConfig.NJsonSchemaGeneratorSettings()).AsSyncOverAsync();
            _deserializer = new JsonDeserializer<MyKafkaCommand>().AsSyncOverAsync();
            _serializationContext = new SerializationContext(MessageComponentType.Value, _topic);
        }

        [Fact]
        public void When_posting_a_message_via_the_messaging_gateway()
        {
            //arrange
            
            var myCommand = new MyKafkaCommand{ Value = "Hello World"};
            
            //use the serdes json serializer to write the message to the topic
            var body = _serializer.Serialize(myCommand, _serializationContext);
            
            //grab the schema id that was written to the message by the serializer
            var schemaId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(body.Skip(1).Take(4).ToArray()));

            var sent = new Message(
                new MessageHeader(Guid.NewGuid(), _topic, MessageType.MT_COMMAND)
                {
                    PartitionKey = _partitionKey
                },
                new MessageBody(body));
            
            //act
            
            ((IAmAMessageProducerSync)_producerRegistry.LookupBy(_topic)).Send(sent);

            var received = GetMessage();

            received.Body.Bytes.Length.Should().BeGreaterThan(5);
            
            var receivedSchemaId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(received.Body.Bytes.Skip(1).Take(4).ToArray()));
            
            var receivedCommand = _deserializer.Deserialize(received.Body.Bytes, received.Body.Bytes is null, _serializationContext);
            
            //assert
            received.Header.MessageType.Should().Be(MessageType.MT_COMMAND);
            received.Header.PartitionKey.Should().Be(_partitionKey);
            received.Body.Bytes.Should().Equal(received.Body.Bytes);
            received.Body.Value.Should().Be(received.Body.Value);
            receivedSchemaId.Should().Be(schemaId);
            receivedCommand.Id.Should().Be(myCommand.Id);
            receivedCommand.Value.Should().Be(myCommand.Value);
        }

        private Message GetMessage()
        {
            Message[] messages = Array.Empty<Message>();
            int maxTries = 0;
            do
            {
                try
                {
                    maxTries++;
                    Task.Delay(500).Wait(); //Let topic propagate in the broker
                    messages = _consumer.Receive(1000);
                    
                    if (messages[0].Header.MessageType != MessageType.MT_NONE)
                    {
                        _consumer.Acknowledge(messages[0]);
                        break;
                    }
                        
                }
                catch (ChannelFailureException cfx)
                {
                    //Lots of reasons to be here as Kafka propagates a topic, or the test cluster is still initializing
                    _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                }

            } while (maxTries <= 3);
            
            if (messages[0].Header.MessageType == MessageType.MT_NONE)
                throw new Exception($"Failed to read from topic:{_topic} after {maxTries} attempts");
            
            return messages[0];
        }

        public void Dispose()
        {
            _producerRegistry?.Dispose();
            _consumer?.Dispose();
        }
    }
}
