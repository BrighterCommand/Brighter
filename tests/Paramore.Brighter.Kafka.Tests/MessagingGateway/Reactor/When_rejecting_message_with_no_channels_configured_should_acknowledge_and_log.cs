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
public class KafkaMessageConsumerNoChannelsTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly KafkaMessageProducer _producer;
    private readonly string _partitionKey = Guid.NewGuid().ToString();

    public KafkaMessageConsumerNoChannelsTests(ITestOutputHelper output)
    {
        _output = output;

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
                Name = "Kafka Producer No Channels Test",
                BootStrapServers = new[] { "localhost:9092" }
            },
            publication);

        _producer.Init();
    }

    [Fact]
    public async Task When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log()
    {
        //Arrange - let topics propagate in the broker
        await Task.Delay(500);

        var groupId = Guid.NewGuid().ToString();
        var routingKey = new RoutingKey(_topic);

        //send two messages to the data topic
        var messageId1 = Guid.NewGuid().ToString();
        var sentMessage1 = new Message(
            new MessageHeader(messageId1, routingKey, MessageType.MT_COMMAND) { PartitionKey = _partitionKey },
            new MessageBody($"test message 1 - should be rejected")
        );
        _producer.Send(sentMessage1);

        var messageId2 = Guid.NewGuid().ToString();
        var sentMessage2 = new Message(
            new MessageHeader(messageId2, routingKey, MessageType.MT_COMMAND) { PartitionKey = _partitionKey },
            new MessageBody($"test message 2 - should be received after rejection")
        );
        _producer.Send(sentMessage2);
        _producer.Flush();

        //Act - consume and reject the first message, then consume the second
        using (var consumer = CreateConsumerWithNoChannels(groupId))
        {
            var receivedMessage1 = ConsumeMessage(consumer);
            Assert.Equal(messageId1, receivedMessage1.Id);

            _output.WriteLine($"About to reject message {messageId1} with no channels configured");

            //reject with no channels configured - should acknowledge and log warning
            var rejected = consumer.Reject(receivedMessage1, new MessageRejectionReason(RejectionReason.DeliveryError, "Test rejection with no channels"));

            _output.WriteLine($"Message {messageId1} rejected, attempting to consume next message");

            //Assert - verify rejection returned true and we can consume the next message
            Assert.True(rejected, "Reject should return true even with no channels");

            //verify we can consume the next message (proving first was acknowledged)
            var receivedMessage2 = ConsumeMessage(consumer);
            Assert.Equal(messageId2, receivedMessage2.Id);

            _output.WriteLine($"Successfully consumed message {messageId2} after rejection");
        }

        //Additional verification: ensure no messages were sent to non-existent DLQ
        //If we had a DLQ, we'd try to consume from it and fail, but since we don't configure one,
        //this test simply verifies that rejection doesn't block the consumer
    }

    private IAmAMessageConsumerSync CreateConsumerWithNoChannels(string groupId)
    {
        return new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer No Channels Test",
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
                makeChannels: OnMissingChannel.Create
                // NOTE: No deadLetterRoutingKey or invalidMessageRoutingKey - this is the key test condition
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
