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

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Proactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class KafkaMessageConsumerInvalidMessageFallbackAsyncTests : IAsyncDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly string _dlqTopic;
    private readonly KafkaMessageProducer _producer;
    private readonly string _partitionKey = Guid.NewGuid().ToString();

    public KafkaMessageConsumerInvalidMessageFallbackAsyncTests(ITestOutputHelper output)
    {
        _output = output;
        _dlqTopic = $"{_topic}.dlq";

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
                Name = "Kafka Producer Invalid Message Fallback Async Test",
                BootStrapServers = new[] { "localhost:9092" }
            },
            publication);

        _producer.Init();
    }

    [Fact]
    public async Task When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq_async()
    {
        //Arrange - let topics propagate in the broker
        await Task.Delay(2000);

        var groupId = Guid.NewGuid().ToString();
        var routingKey = new RoutingKey(_topic);
        var dlqRoutingKey = new RoutingKey(_dlqTopic);

        //send a message to the data topic
        var messageId = Guid.NewGuid().ToString();
        var sentMessage = new Message(
            new MessageHeader(messageId, routingKey, MessageType.MT_COMMAND) { PartitionKey = _partitionKey },
            new MessageBody($"test content for invalid message fallback async")
        );
        await _producer.SendAsync(sentMessage);
        _producer.Flush();

        //allow message to propagate
        await Task.Delay(5000);

        //Act - consume and reject the message with Unacceptable reason
        Message? receivedMessage;
        await using (var consumer = CreateConsumerWithDlqOnly(groupId, dlqRoutingKey))
        {
            receivedMessage = await ConsumeMessageAsync(consumer);
            Assert.Equal(messageId, receivedMessage.Id);

            _output.WriteLine($"About to reject message {messageId} with Unacceptable reason (no invalid channel configured)");

            //reject with Unacceptable reason - should fall back to DLQ since no invalid channel configured
            await consumer.RejectAsync(receivedMessage, new MessageRejectionReason(RejectionReason.Unacceptable, "Test unacceptable message fallback async"));

            _output.WriteLine($"Message {messageId} rejected, waiting for DLQ propagation");

            //yield to allow DLQ message to be produced and topic to be created
            await Task.Delay(TimeSpan.FromMilliseconds(3000));
        }

        _output.WriteLine("Creating DLQ consumer");

        //yield to allow DLQ topic to propagate
        await Task.Delay(TimeSpan.FromMilliseconds(1000));

        //Assert - verify message appears on DLQ (not invalid message channel)
        await using (var dlqConsumer = CreateDLQConsumer(groupId))
        {
            _output.WriteLine("Attempting to consume from DLQ");
            var dlqMessage = await ConsumeMessageAsync(dlqConsumer);

            Assert.NotNull(dlqMessage);
            Assert.Equal(MessageType.MT_COMMAND, dlqMessage.Header.MessageType);
            Assert.Equal(receivedMessage.Body.Value, dlqMessage.Body.Value);

            //verify rejection metadata was added
            Assert.True(dlqMessage.Header.Bag.ContainsKey("OriginalTopic"));
            Assert.Equal(_topic, dlqMessage.Header.Bag["OriginalTopic"]);
            Assert.True(dlqMessage.Header.Bag.ContainsKey("RejectionReason"));
            Assert.Equal("Unacceptable", dlqMessage.Header.Bag["RejectionReason"]);
        }
    }

    private IAmAMessageConsumerAsync CreateConsumerWithDlqOnly(string groupId, RoutingKey dlqRoutingKey)
    {
        return new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer Invalid Message Fallback Async Test",
                    BootStrapServers = new[] { "localhost:9092" }
                })
            .CreateAsync(new KafkaSubscription<MyCommand>
            (
                subscriptionName: new SubscriptionName("Paramore.Brighter.Tests"),
                channelName: new ChannelName(_queueName),
                routingKey: new RoutingKey(_topic),
                groupId: groupId,
                commitBatchSize: 1,
                numOfPartitions: 1,
                replicationFactor: 1,
                messagePumpType: MessagePumpType.Proactor,
                makeChannels: OnMissingChannel.Create,
                deadLetterRoutingKey: dlqRoutingKey
                // NOTE: No invalidMessageRoutingKey - this is the key test condition
            ));
    }

    private IAmAMessageConsumerAsync CreateDLQConsumer(string groupId)
    {
        return new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka DLQ Consumer Async Test",
                    BootStrapServers = new[] { "localhost:9092" }
                })
            .CreateAsync(new KafkaSubscription<MyCommand>
            (
                subscriptionName: new SubscriptionName("Paramore.Brighter.DLQ.Tests"),
                channelName: new ChannelName($"{_queueName}.dlq"),
                routingKey: new RoutingKey(_dlqTopic),
                groupId: $"{groupId}.dlq",
                commitBatchSize: 1,
                numOfPartitions: 1,
                replicationFactor: 1,
                messagePumpType: MessagePumpType.Proactor,
                makeChannels: OnMissingChannel.Create
            ));
    }

    private async Task<Message> ConsumeMessageAsync(IAmAMessageConsumerAsync consumer)
    {
        int maxTries = 0;
        do
        {
            try
            {
                maxTries++;
                var messages = await consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));

                if (messages[0].Header.MessageType != MessageType.MT_NONE)
                {
                    return messages[0];
                }
            }
            catch (ChannelFailureException cfx)
            {
                _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                await Task.Delay(1000);
            }
        } while (maxTries <= 10);

        throw new Exception($"Failed to read from topic:{_topic} after {maxTries} attempts");
    }

    public async ValueTask DisposeAsync()
    {
        _producer?.Dispose();
        await Task.CompletedTask;
    }
}
