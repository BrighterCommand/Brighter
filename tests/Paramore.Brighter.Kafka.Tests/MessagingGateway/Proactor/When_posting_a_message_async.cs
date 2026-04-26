using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Proactor;

[Category("Kafka")]
public class KafkaMessageProducerSendTestsAsync : IAsyncDisposable
{
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private IAmAProducerRegistry _producerRegistry;
    private IAmAMessageConsumerAsync _consumer;
    private readonly string _partitionKey = Guid.NewGuid().ToString();

    [Before(Test)]
    public async Task Setup()
    {
        string groupId = Guid.NewGuid().ToString();
        _producerRegistry = await new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Producer Send Test", BootStrapServers = new[] { "localhost:9092" }
            },
            [
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
            ]).CreateAsync();

        _consumer = new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer Test", BootStrapServers = new[] { "localhost:9092" }
                })
            .CreateAsync(new KafkaSubscription<MyCommand>(
                    channelName: new ChannelName(_queueName),
                    routingKey: new RoutingKey(_topic),
                    groupId: groupId,
                    messagePumpType: MessagePumpType.Proactor,
                    numOfPartitions: 1, replicationFactor: 1, makeChannels: OnMissingChannel.Create)
            );
    }

    //[Test, Skip("As it has to wait for the messages to flush, only tends to run well in debug")]
    [Test]
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
                ContentType = new ContentType(MediaTypeNames.Application.Json),
                Bag = new Dictionary<string, object>{{"Test Header", "Test Value"},},
                ReplyTo = new RoutingKey("com.brightercommand.replyto"),
                CorrelationId = Guid.NewGuid().ToString(),
                Delayed = TimeSpan.FromMilliseconds(10),
                HandledCount = 2,
                TimeStamp = DateTime.UtcNow
            },
            new MessageBody(body));

        bool messagePublished = false;
        var producerAsync = _producerRegistry.LookupAsyncBy(routingKey);
        var producerConfirm = producerAsync as ISupportPublishConfirmation;
        producerConfirm.OnMessagePublished += delegate(bool success, string id)
        {
            if (success) messagePublished = true;
        };
        await producerAsync.SendAsync(message);
        
        //We should not need to flush, as the async does not queue work  - but in case this changes
        ((KafkaMessageProducer)producerAsync).Flush();

        //allow the message publication callback to run
        await Task.Delay(10000);

        await Assert.That(messagePublished).IsTrue();

        var receivedMessage = await GetMessageAsync();

        var receivedCommand = JsonSerializer.Deserialize<MyCommand>(receivedMessage.Body.Value, JsonSerialisationOptions.Options);

        await Assert.That(receivedMessage.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        await Assert.That(receivedMessage.Header.PartitionKey).IsEqualTo(_partitionKey);
        await Assert.That(receivedMessage.Body.Bytes).IsEquivalentTo(message.Body.Bytes);
        await Assert.That(receivedMessage.Body.Value).IsEqualTo(message.Body.Value);
        await Assert.That(receivedMessage.Header.TimeStamp).IsEqualTo(message.Header.TimeStamp).Within(TimeSpan.FromSeconds(5));
        await Assert.That(receivedCommand.Id).IsEqualTo(command.Id);
        await Assert.That(receivedCommand.Value).IsEqualTo(command.Value);
    }

    private async Task<Message> GetMessageAsync()
    {
        Message[] messages = [];
        int maxTries = 0;
        do
        {
            try
            {
                maxTries++;
 
                //set timespan to zero so that we will not block
                messages = await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));

                if (messages[0].Header.MessageType != MessageType.MT_NONE)
                {
                    await _consumer.AcknowledgeAsync(messages[0]);
                    break;
                }

                //wait before retry
                await Task.Delay(1000);
            }
            catch (ChannelFailureException cfx)
            {
                //Lots of reasons to be here as Kafka propagates a topic, or the test cluster is still initializing
                Console.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                await Task.Delay(1000);
            }
        } while (maxTries <= 10);

        if (messages[0].Header.MessageType == MessageType.MT_NONE)
            throw new Exception($"Failed to read from topic:{_topic} after {maxTries} attempts");

        return messages[0];
    }
    
    [After(Test)]
    public async Task Cleanup()
    {
        _producerRegistry?.Dispose();
        ((IAmAMessageConsumerSync)_consumer)?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
            _producerRegistry.Dispose();
            await _consumer.DisposeAsync();
    }
}

