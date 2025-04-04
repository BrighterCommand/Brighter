using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")] //Kafka doesn't like multiple consumers of a partition
public class KafkaMessageProducerSendTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly IAmAMessageConsumerSync _consumer;
    private readonly string _partitionKey = Guid.NewGuid().ToString();

    public KafkaMessageProducerSendTests(ITestOutputHelper output)
    {
        const string groupId = "Kafka Message Producer Send Test";
        _output = output;
        _producerRegistry = new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration { Name = "Kafka Producer Send Test", BootStrapServers = new[] { "localhost:9092" } },
            new[]
            {
                new KafkaPublication
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
            }).Create();

        _consumer = new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration { Name = "Kafka Consumer Test", BootStrapServers = new[] { "localhost:9092" } })
            .Create(new KafkaSubscription<MyCommand>(
                    channelName: new ChannelName(_queueName),
                    routingKey: new RoutingKey(_topic),
                    groupId: groupId,
                    numOfPartitions: 1,
                    replicationFactor: 1,
                    messagePumpType: MessagePumpType.Reactor,
                    makeChannels: OnMissingChannel.Create
                )
            );
    }

    [Fact]
    public async Task When_posting_a_message()
    {
        //Let topic propagate in the broker
        await Task.Delay(500);

        var command = new MyCommand { Value = "Test Content" };

        //vanilla i.e. no Kafka specific bytes at the beginning
        var body = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);

        var routingKey = new RoutingKey(_topic);

        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND)
            {
                PartitionKey = _partitionKey,
                ContentType = "application/json",
                Bag = new Dictionary<string, object> { { "Test Header", "Test Value" }, },
                ReplyTo = "com.brightercommand.replyto",
                CorrelationId = Guid.NewGuid().ToString(),
                Delayed = TimeSpan.FromMilliseconds(10),
                HandledCount = 2,
                TimeStamp = DateTime.UtcNow
            }, 
            new MessageBody(body));

        bool messagePublished = false;
        var producer = ((IAmAMessageProducerSync)_producerRegistry.LookupBy(routingKey));
        producer.Send(message);
        var producerConfirm = producer as ISupportPublishConfirmation;
        producerConfirm.OnMessagePublished += delegate(bool success, string id)
        {
            if (success) messagePublished = true;
        };

        //ensure that the messages have flushed
        ((KafkaMessageProducer)producer).Flush();

        //allow propagation of callback
        await Task.Delay(1000);

        Assert.True(messagePublished);

        var receivedMessage = GetMessage();

        var receivedCommand = JsonSerializer.Deserialize<MyCommand>(receivedMessage.Body.Value, JsonSerialisationOptions.Options);

        Assert.Equal(MessageType.MT_COMMAND, receivedMessage.Header.MessageType);
        Assert.Equal(_partitionKey, receivedMessage.Header.PartitionKey);
        Assert.Equal(message.Body.Bytes, receivedMessage.Body.Bytes);
        Assert.Equal(message.Body.Value, receivedMessage.Body.Value);
        Assert.Equal(message.Header.TimeStamp.ToString("u"), receivedMessage.Header.TimeStamp.ToString("u"));
        Assert.Equal(command.Id, receivedCommand.Id);
        Assert.Equal(command.Value, receivedCommand.Value);
    }

    private Message GetMessage()
    {
        Message[] messages = [];
        int maxTries = 0;
        do
        {
            try
            {
                maxTries++;
                messages = _consumer.Receive(TimeSpan.FromMilliseconds(1000));

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
