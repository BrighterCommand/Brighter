using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Category("Kafka")]
public class KafkaMessageProducerSendTests : IDisposable
{
    private readonly string _channelName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly IAmAMessageConsumerSync _consumer;
    private readonly string _partitionKey = Guid.NewGuid().ToString();

    public KafkaMessageProducerSendTests()
    {
        string groupId = Guid.NewGuid().ToString();
        _producerRegistry = new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration { Name = "Kafka Producer Send Test", BootStrapServers = new[] { "localhost:9092" } },
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
            ]).Create();

        _consumer = new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration { Name = "Kafka Consumer Test", BootStrapServers = new[] { "localhost:9092" } })
            .Create(new KafkaSubscription<MyCommand>(
                    channelName: new ChannelName(_channelName),
                    routingKey: new RoutingKey(_topic),
                    groupId: groupId,
                    numOfPartitions: 1,
                    replicationFactor: 1,
                    messagePumpType: MessagePumpType.Reactor,
                    makeChannels: OnMissingChannel.Create
                )
            );
    }

    [Test]
    public async Task When_posting_a_message()
    {
        //Let topic propagate in the broker
        await Task.Delay(500);

        var command = new MyCommand { Value = "Test Content" };

        //vanilla i.e. no Kafka specific bytes at the beginning
        var body = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);

        var routingKey = new RoutingKey(_topic);

        var correlationId = Guid.NewGuid().ToString();
        var messageId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow;
        var contentType = new ContentType(MediaTypeNames.Application.Json){CharSet = CharacterEncoding.UTF8.FromCharacterEncoding()};
        const string replyTo = "reply-queue";
        var  type = new CloudEventsType("test-type");
        const string subject = "test-subject";
        var source = new Uri("http://testing.com");
        var schema = new Uri("http://schema.com");
        const string traceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
        const string traceState = "congo=t61rcWkgMzE";
        var baggage = new Baggage();
        baggage.LoadBaggage("userId=alice,serverNode=DF:28,isProduction=false");
        var delayMilliseconds = TimeSpan.FromMilliseconds(5000);

        var header = new MessageHeader(
            messageId: messageId, 
            topic: routingKey, 
            messageType: MessageType.MT_COMMAND,
            source: source,
            type: type,
            timeStamp: timestamp,
            correlationId: correlationId,
            replyTo: new RoutingKey(replyTo),
            contentType: contentType,
            partitionKey: _partitionKey,
            handledCount: 0,
            dataSchema: schema,
            subject: subject,
            delayed: delayMilliseconds,
            traceParent: traceParent,
            traceState: traceState,
            baggage: baggage
        );

        header.Bag = new Dictionary<string, object> { { "Test Header", "Test Value" }, };

        var message = new Message(header, new MessageBody(body));
        
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

        await Assert.That(messagePublished).IsTrue();

        var receivedMessage = await GetMessage();

        var receivedCommand = JsonSerializer.Deserialize<MyCommand>(receivedMessage.Body.Value, JsonSerialisationOptions.Options);

        await Assert.That(receivedMessage.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        await Assert.That(receivedMessage.Header.PartitionKey).IsEqualTo(_partitionKey);
        await Assert.That(receivedMessage.Body.Bytes).IsEquivalentTo(message.Body.Bytes);
        await Assert.That(receivedMessage.Body.Value).IsEqualTo(message.Body.Value);
        await Assert.That(receivedCommand.Id).IsEqualTo(command.Id);
        await Assert.That(receivedCommand.Value).IsEqualTo(command.Value);
        
        // Assert header values
        await Assert.That(receivedMessage.Header.MessageId).IsEqualTo(message.Header.MessageId);
        await Assert.That(receivedMessage.Header.Topic).IsEqualTo(message.Header.Topic);
        await Assert.That(receivedMessage.Header.MessageType).IsEqualTo(message.Header.MessageType);
        await Assert.That(receivedMessage.Header.Source).IsEqualTo(message.Header.Source);
        await Assert.That(receivedMessage.Header.Type).IsEqualTo(message.Header.Type);
        await Assert.That(receivedMessage.Header.TimeStamp).IsEqualTo(message.Header.TimeStamp).Within(TimeSpan.FromSeconds(1));
        await Assert.That(receivedMessage.Header.CorrelationId).IsEqualTo(message.Header.CorrelationId);
        await Assert.That(receivedMessage.Header.ReplyTo).IsEqualTo(message.Header.ReplyTo);
        await Assert.That(receivedMessage.Header.ContentType).IsEqualTo(message.Header.ContentType);
        await Assert.That(receivedMessage.Header.HandledCount).IsEqualTo(message.Header.HandledCount);
        await Assert.That(receivedMessage.Header.DataSchema).IsEqualTo(message.Header.DataSchema);
        await Assert.That(receivedMessage.Header.Subject).IsEqualTo(message.Header.Subject);
        await Assert.That(receivedMessage.Header.Delayed).IsEqualTo(delayMilliseconds);                                //we clear any delay from the producer, as it represents delay in the pipeline
        await Assert.That(receivedMessage.Header.TraceParent).IsEqualTo(message.Header.TraceParent);
        await Assert.That(receivedMessage.Header.TraceState).IsEqualTo(message.Header.TraceState);
        await Assert.That(receivedMessage.Header.Baggage).IsEqualTo(message.Header.Baggage);
        await Assert.That(message.Header.Bag.ContainsKey("Test Header")).IsTrue();
        await Assert.That(message.Header.Bag["Test Header"]).IsEqualTo("Test Value");
    }

    private async Task<Message> GetMessage()
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
                Console.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                await Task.Delay(1000);
            }
        } while (maxTries <= 10);

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

