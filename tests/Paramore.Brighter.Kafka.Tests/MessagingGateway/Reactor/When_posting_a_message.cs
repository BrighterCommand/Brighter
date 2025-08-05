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
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")] //Kafka doesn't like multiple consumers of a partition
public class KafkaMessageProducerSendTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _channelName = Guid.NewGuid().ToString();
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

    [Fact]
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

        Assert.True(messagePublished);

        var receivedMessage = GetMessage();

        var receivedCommand = JsonSerializer.Deserialize<MyCommand>(receivedMessage.Body.Value, JsonSerialisationOptions.Options);

        Assert.Equal(MessageType.MT_COMMAND, receivedMessage.Header.MessageType);
        Assert.Equal(_partitionKey, receivedMessage.Header.PartitionKey);
        Assert.Equal(message.Body.Bytes, receivedMessage.Body.Bytes);
        Assert.Equal(message.Body.Value, receivedMessage.Body.Value);
        Assert.Equal(command.Id, receivedCommand.Id);
        Assert.Equal(command.Value, receivedCommand.Value);
        
        // Assert header values
        Assert.Equal(message.Header.MessageId, receivedMessage.Header.MessageId);
        Assert.Equal(message.Header.Topic, receivedMessage.Header.Topic);
        Assert.Equal(message.Header.MessageType, receivedMessage.Header.MessageType);
        Assert.Equal(message.Header.Source,receivedMessage.Header.Source);
        Assert.Equal(message.Header.Type,receivedMessage.Header.Type);
        Assert.Equal(message.Header.TimeStamp, receivedMessage.Header.TimeStamp, TimeSpan.FromSeconds(1));
        Assert.Equal(message.Header.CorrelationId,receivedMessage.Header.CorrelationId);
        Assert.Equal(message.Header.ReplyTo, receivedMessage.Header.ReplyTo);
        Assert.Equal(message.Header.ContentType,receivedMessage.Header.ContentType);
        Assert.Equal(message.Header.HandledCount, receivedMessage.Header.HandledCount);
        Assert.Equal(message.Header.DataSchema,receivedMessage.Header.DataSchema);
        Assert.Equal(message.Header.Subject,receivedMessage.Header.Subject);
        Assert.Equal(delayMilliseconds, receivedMessage.Header.Delayed);                                //we clear any delay from the producer, as it represents delay in the pipeline 
        Assert.Equal(message.Header.TraceParent,receivedMessage.Header.TraceParent);
        Assert.Equal(message.Header.TraceState, receivedMessage.Header.TraceState);
        Assert.Equal(message.Header.Baggage, receivedMessage.Header.Baggage);
        Assert.True(message.Header.Bag.ContainsKey("Test Header"));
        Assert.Equal("Test Value", message.Header.Bag["Test Header"]);
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
