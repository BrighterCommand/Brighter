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
public class KafkaMessageConsumerMetadataTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly string _dlqTopic;
    private readonly KafkaMessageProducer _producer;
    private readonly string _partitionKey = Guid.NewGuid().ToString();

    public KafkaMessageConsumerMetadataTests(ITestOutputHelper output)
    {
        _output = output;
        _dlqTopic = $"{_topic}.dlq";

        //Arrange - create producer for the data topic
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
                Name = "Kafka Producer Metadata Test",
                BootStrapServers = new[] { "localhost:9092" }
            },
            publication);

        _producer.Init();
    }

    [Fact]
    public async Task When_rejecting_message_should_include_metadata()
    {
        //Arrange - let topics propagate in the broker
        await Task.Delay(500);

        var groupId = Guid.NewGuid().ToString();
        var routingKey = new RoutingKey(_topic);
        var dlqRoutingKey = new RoutingKey(_dlqTopic);

        //send a message to the data topic
        var messageId = Guid.NewGuid().ToString();
        var sentMessage = new Message(
            new MessageHeader(messageId, routingKey, MessageType.MT_COMMAND) { PartitionKey = _partitionKey },
            new MessageBody($"test content for metadata verification")
        );
        _producer.Send(sentMessage);
        _producer.Flush();

        //Act - consume and reject the message with a description
        var rejectionDescription = "Test rejection with metadata";
        Message? receivedMessage;
        using (var consumer = CreateConsumer(groupId, dlqRoutingKey))
        {
            receivedMessage = ConsumeMessage(consumer);
            Assert.Equal(messageId, receivedMessage.Id);

            _output.WriteLine($"About to reject message {messageId} with DeliveryError");

            //reject with DeliveryError reason and description
            consumer.Reject(receivedMessage, new MessageRejectionReason(RejectionReason.DeliveryError, rejectionDescription));

            _output.WriteLine($"Message {messageId} rejected, waiting for DLQ propagation");

            //yield to allow DLQ message to be produced and topic to be created
            await Task.Delay(TimeSpan.FromMilliseconds(3000));
        }

        _output.WriteLine("Creating DLQ consumer to verify metadata");

        //yield to allow DLQ topic to propagate
        await Task.Delay(TimeSpan.FromMilliseconds(1000));

        //Assert - verify message appears on DLQ with all required metadata
        using (var dlqConsumer = CreateDLQConsumer(groupId))
        {
            _output.WriteLine("Attempting to consume from DLQ");
            var dlqMessage = ConsumeMessage(dlqConsumer);

            Assert.NotNull(dlqMessage);
            Assert.Equal(receivedMessage.Body.Value, dlqMessage.Body.Value);

            //verify OriginalTopic metadata
            Assert.True(dlqMessage.Header.Bag.ContainsKey(HeaderNames.ORIGINAL_TOPIC), "OriginalTopic metadata missing");
            Assert.Equal(_topic, dlqMessage.Header.Bag[HeaderNames.ORIGINAL_TOPIC]);
            _output.WriteLine($"✓ OriginalTopic: {dlqMessage.Header.Bag[HeaderNames.ORIGINAL_TOPIC]}");

            //verify RejectionTimestamp metadata
            Assert.True(dlqMessage.Header.Bag.ContainsKey(HeaderNames.REJECTION_TIMESTAMP), "RejectionTimestamp metadata missing");
            var rejectionTimestamp = dlqMessage.Header.Bag[HeaderNames.REJECTION_TIMESTAMP] as string;
            Assert.NotNull(rejectionTimestamp);
            Assert.True(DateTimeOffset.TryParse(rejectionTimestamp, out var parsedTimestamp), "RejectionTimestamp should be parseable ISO format");
            Assert.True(DateTimeOffset.UtcNow - parsedTimestamp < TimeSpan.FromMinutes(1), "RejectionTimestamp should be recent");
            _output.WriteLine($"✓ RejectionTimestamp: {rejectionTimestamp}");

            //verify RejectionReason metadata
            Assert.True(dlqMessage.Header.Bag.ContainsKey(HeaderNames.REJECTION_REASON), "RejectionReason metadata missing");
            Assert.Equal("DeliveryError", dlqMessage.Header.Bag[HeaderNames.REJECTION_REASON]);
            _output.WriteLine($"✓ RejectionReason: {dlqMessage.Header.Bag[HeaderNames.REJECTION_REASON]}");

            //verify RejectionMessage metadata (optional description)
            Assert.True(dlqMessage.Header.Bag.ContainsKey(HeaderNames.REJECTION_MESSAGE), "RejectionMessage metadata missing");
            Assert.Equal(rejectionDescription, dlqMessage.Header.Bag[HeaderNames.REJECTION_MESSAGE]);
            _output.WriteLine($"✓ RejectionMessage: {dlqMessage.Header.Bag[HeaderNames.REJECTION_MESSAGE]}");

            //verify MessageType metadata
            Assert.True(dlqMessage.Header.Bag.ContainsKey(HeaderNames.ORIGINAL_TYPE), "MessageType metadata missing");
            Assert.Equal("MT_COMMAND", dlqMessage.Header.Bag[HeaderNames.ORIGINAL_TYPE]);
            _output.WriteLine($"✓ MessageType: {dlqMessage.Header.Bag[HeaderNames.ORIGINAL_TYPE]}");

            _output.WriteLine("All metadata fields verified successfully");
        }
    }

    private IAmAMessageConsumerSync CreateConsumer(string groupId, RoutingKey dlqRoutingKey)
    {
        return new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer Metadata Test",
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
                deadLetterRoutingKey: dlqRoutingKey
            ));
    }

    private IAmAMessageConsumerSync CreateDLQConsumer(string groupId)
    {
        return new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka DLQ Consumer Test",
                    BootStrapServers = new[] { "localhost:9092" }
                })
            .Create(new KafkaSubscription<MyCommand>
            (
                subscriptionName: new SubscriptionName("Paramore.Brighter.DLQ.Tests"),
                channelName: new ChannelName($"{_queueName}.dlq"),
                routingKey: new RoutingKey(_dlqTopic),
                groupId: $"{groupId}.dlq",
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
