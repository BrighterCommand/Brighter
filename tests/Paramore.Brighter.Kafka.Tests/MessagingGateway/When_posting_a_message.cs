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
using System.Text.Json;
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
    public class KafkaMessageProducerSendTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _queueName = Guid.NewGuid().ToString();
        private readonly string _topic = Guid.NewGuid().ToString();
        private readonly IAmAProducerRegistry _producerRegistry;
        private readonly IAmAMessageConsumer _consumer;
        private readonly string _partitionKey = Guid.NewGuid().ToString();


        public KafkaMessageProducerSendTests(ITestOutputHelper output)
        {
            const string groupId = "Kafka Message Producer Send Test";
            _output = output;
            _producerRegistry = new KafkaProducerRegistryFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Producer Send Test",
                    BootStrapServers = new[] {"localhost:9092"}
                },
                new KafkaPublication[] {new KafkaPublication()
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

        }

        [Fact]
        public void When_posting_a_message()
        {
            var command = new MyCommand{Value = "Test Content"};

            //vanilla i.e. no Kafka specific bytes at the beginning
            var body = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);

            var message = new Message(
                new MessageHeader(Guid.NewGuid(), _topic, MessageType.MT_COMMAND)
                {
                    PartitionKey = _partitionKey
                },
                new MessageBody(body));

            ((IAmAMessageProducerSync)_producerRegistry.LookupBy(_topic)).Send(message);

            var receivedMessage = GetMessage();

            var receivedCommand = JsonSerializer.Deserialize<MyCommand>(message.Body.Value, JsonSerialisationOptions.Options);

            receivedMessage.Header.MessageType.Should().Be(MessageType.MT_COMMAND);
            receivedMessage.Header.PartitionKey.Should().Be(_partitionKey);
            receivedMessage.Body.Bytes.Should().Equal(message.Body.Bytes);
            receivedMessage.Body.Value.Should().Be(message.Body.Value);
            receivedCommand.Id.Should().Be(command.Id);
            receivedCommand.Value.Should().Be(command.Value);
        }

        private Message GetMessage()
        {
            Message[] messages = new Message[0];
            int maxTries = 0;
            do
            {
                try
                {
                    maxTries++;
                    Task.Delay(500).Wait(); //Let topic propogate in the broker
                    messages = _consumer.Receive(1000);

                    if (messages[0].Header.MessageType != MessageType.MT_NONE)
                    {
                        _consumer.Acknowledge(messages[0]);
                        break;
                    }

                }
                catch (ChannelFailureException cfx)
                {
                    //Lots of reasons to be here as Kafka propogates a topic, or the test cluster is still initializing
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
