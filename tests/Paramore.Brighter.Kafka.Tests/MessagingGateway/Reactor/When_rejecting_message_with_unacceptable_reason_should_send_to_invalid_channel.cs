#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Threading.Tasks;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class KafkaMessageConsumerInvalidMessageTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly string _invalidMessageTopic;
    private readonly KafkaMessageProducer _producer;
    private readonly string _partitionKey = Guid.NewGuid().ToString();

    public KafkaMessageConsumerInvalidMessageTests(ITestOutputHelper output)
    {
        _output = output;
        _invalidMessageTopic = $"{_topic}.invalid";

        // Create producer directly for the data topic
        var publication = new KafkaPublication
        {
            Topic = new RoutingKey(_topic),
            NumPartitions = 1,
            ReplicationFactor = 1,
            MessageTimeoutMs = 2000,
            RequestTimeoutMs = 2000,
            MakeChannels = OnMissingChannel.Create
        };

        _producer = new KafkaMessageProducer(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Producer Invalid Message Test",
                BootStrapServers = new[] { "localhost:9092" }
            },
            publication);

        _producer.Init();
    }

    [Fact]
    public async Task When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel()
    {
        //Arrange - let topics propagate in the broker
        await Task.Delay(500);

        var groupId = Guid.NewGuid().ToString();
        var routingKey = new RoutingKey(_topic);
        var invalidMessageRoutingKey = new RoutingKey(_invalidMessageTopic);

        //send a message to the data topic
        var messageId = Guid.NewGuid().ToString();
        var sentMessage = new Message(
            new MessageHeader(messageId, routingKey, MessageType.MT_COMMAND) { PartitionKey = _partitionKey },
            new MessageBody($"test content for invalid message channel")
        );
        _producer.Send(sentMessage);
        _producer.Flush();

        //Act - consume and reject the message
        Message? receivedMessage;
        using (var consumer = CreateConsumer(groupId, invalidMessageRoutingKey))
        {
            receivedMessage = ConsumeMessage(consumer);
            Assert.Equal(messageId, receivedMessage.Id);

            _output.WriteLine($"About to reject message {messageId} with Unacceptable reason");

            //reject with Unacceptable reason
            consumer.Reject(receivedMessage, new MessageRejectionReason(RejectionReason.Unacceptable, "Test unacceptable message"));

            _output.WriteLine($"Message {messageId} rejected, waiting for invalid message channel propagation");

            //yield to allow invalid message to be produced and topic to be created
            await Task.Delay(TimeSpan.FromMilliseconds(3000));
        }

        _output.WriteLine("Creating invalid message channel consumer");

        //yield to allow invalid message topic to propagate
        await Task.Delay(TimeSpan.FromMilliseconds(1000));

        //Assert - verify message appears on invalid message channel
        using (var invalidMessageConsumer = CreateInvalidMessageConsumer(groupId))
        {
            _output.WriteLine("Attempting to consume from invalid message channel");
            var invalidMessage = ConsumeMessage(invalidMessageConsumer);

            Assert.NotNull(invalidMessage);
            Assert.Equal(MessageType.MT_COMMAND, invalidMessage.Header.MessageType);
            Assert.Equal(receivedMessage.Body.Value, invalidMessage.Body.Value);

            //verify rejection metadata was added
            Assert.True(invalidMessage.Header.Bag.ContainsKey("OriginalTopic"));
            Assert.Equal(_topic, invalidMessage.Header.Bag["OriginalTopic"]);
            Assert.True(invalidMessage.Header.Bag.ContainsKey("RejectionReason"));
            Assert.Equal("Unacceptable", invalidMessage.Header.Bag["RejectionReason"]);
        }
    }

    private IAmAMessageConsumerSync CreateConsumer(string groupId, RoutingKey invalidMessageRoutingKey)
    {
        return new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer Invalid Message Test",
                    BootStrapServers = new[] { "localhost:9092" }
                })
            .Create(new KafkaSubscription<MyCommand>
            (
                subscriptionName: new SubscriptionName("Paramore.Brighter.Tests"),
                channelName: new ChannelName(_queueName),
                routingKey: new RoutingKey(_topic),
                groupId: groupId,
                commitBatchSize: 1,
                numOfPartitions: 1,
                replicationFactor: 1,
                messagePumpType: MessagePumpType.Reactor,
                makeChannels: OnMissingChannel.Create,
                invalidMessageRoutingKey: invalidMessageRoutingKey
            ));
    }

    private IAmAMessageConsumerSync CreateInvalidMessageConsumer(string groupId)
    {
        return new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Invalid Message Consumer Test",
                    BootStrapServers = new[] { "localhost:9092" }
                })
            .Create(new KafkaSubscription<MyCommand>
            (
                subscriptionName: new SubscriptionName("Paramore.Brighter.InvalidMessage.Tests"),
                channelName: new ChannelName($"{_queueName}.invalid"),
                routingKey: new RoutingKey(_invalidMessageTopic),
                groupId: $"{groupId}.invalid",
                commitBatchSize: 1,
                numOfPartitions: 1,
                replicationFactor: 1,
                messagePumpType: MessagePumpType.Reactor,
                makeChannels: OnMissingChannel.Create
            ));
    }

    private Message ConsumeMessage(IAmAMessageConsumerSync consumer)
    {
        int maxTries = 0;
        do
        {
            try
            {
                maxTries++;
                var messages = consumer.Receive(TimeSpan.FromMilliseconds(1000));

                if (messages[0].Header.MessageType != MessageType.MT_NONE)
                {
                    return messages[0];
                }
            }
            catch (ChannelFailureException cfx)
            {
                _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                Task.Delay(1000).GetAwaiter().GetResult();
            }
        } while (maxTries <= 10);

        throw new Exception($"Failed to read from topic:{_topic} after {maxTries} attempts");
    }

    public void Dispose()
    {
        _producer?.Dispose();
    }
}
