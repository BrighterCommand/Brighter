using System;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Proactor;

[Category("Kafka")]
public class KafkaMessageProducerHeaderBytesSendTestsAsync : IAsyncDisposable
{
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private IAmAProducerRegistry _producerRegistry;
    private IAmAMessageConsumerAsync _consumer;
    private readonly string _partitionKey = Guid.NewGuid().ToString();
    private IAsyncSerializer<MyKafkaCommand> _serializer;
    private IAsyncDeserializer<MyKafkaCommand> _deserializer;
    private SerializationContext _serializationContext;

    [Before(Test)]
    public async Task Setup()
    {
        string groupId = Guid.NewGuid().ToString();
        _producerRegistry = await new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Producer Send Test",
                BootStrapServers = new[] {"localhost:9092"}
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
                    Name = "Kafka Consumer Test",
                    BootStrapServers = new[] { "localhost:9092" }
                })
            .CreateAsync(new KafkaSubscription<MyCommand>(
                    channelName: new ChannelName(_queueName),
                    routingKey: new RoutingKey(_topic),
                    groupId: groupId,
                    messagePumpType: MessagePumpType.Proactor,
                    numOfPartitions: 1, replicationFactor: 1, makeChannels: OnMissingChannel.Create));

        var schemaRegistryConfig = new SchemaRegistryConfig { Url = "http://localhost:8081"};
        ISchemaRegistryClient schemaRegistryClient = new CachedSchemaRegistryClient(schemaRegistryConfig);

        _serializer = new JsonSerializer<MyKafkaCommand>(schemaRegistryClient, ConfluentJsonSerializationConfig.SerdesJsonSerializerConfig(),
            ConfluentJsonSerializationConfig.NJsonSchemaGeneratorSettings());
        _deserializer = new JsonDeserializer<MyKafkaCommand>();
        _serializationContext = new SerializationContext(MessageComponentType.Value, _topic);
    }

    //[Test, Skip("As it has to wait for the messages to flush, only tends to run well in debug")]
    [Test]
    public async Task When_posting_a_message_via_the_messaging_gateway()
    {
        //Let topic propagate in the broker
        await Task.Delay(500); 
        
        //arrange

        var myCommand = new MyKafkaCommand{ Value = "Hello World"};

        //use the serdes json serializer to write the message to the topic
        var body = await _serializer.SerializeAsync(myCommand, _serializationContext);

        //grab the schema id that was written to the message by the serializer
        var schemaId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(body.Skip(1).Take(4).ToArray()));

        var routingKey = new RoutingKey(_topic);

        var sent = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND, contentType: new ContentType(MediaTypeNames.Application.Octet))
            {
                PartitionKey = _partitionKey
            },
            new MessageBody(body));

        //act

        var producerAsync = ((IAmAMessageProducerAsync)_producerRegistry.LookupAsyncBy(routingKey));
        await producerAsync.SendAsync(sent);
        
        //We should not need to flush, as the async does not queue work  - but in case this changes
        ((KafkaMessageProducer)producerAsync).Flush();

        //let messages propogate on the broker
        await Task.Delay(3000);

        var received = await GetMessageAsync();

        await Assert.That(received.Body.Bytes.Length > 5).IsTrue();

        var receivedSchemaId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(received.Body.Bytes.Skip(1).Take(4).ToArray()));

        var receivedCommand =await  _deserializer.DeserializeAsync(received.Body.Bytes, received.Body.Bytes is null, _serializationContext);

        //assert
        await Assert.That(received.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        await Assert.That(received.Header.PartitionKey).IsEqualTo(_partitionKey);
        await Assert.That(received.Body.Bytes).IsEquivalentTo(body);
        await Assert.That(receivedSchemaId).IsEqualTo(schemaId);
        await Assert.That(receivedCommand.Id).IsEqualTo(myCommand.Id);
        await Assert.That(receivedCommand.Value).IsEqualTo(myCommand.Value);
    }

    private async Task<Message> GetMessageAsync()
    {
        Message[] messages = Array.Empty<Message>();
        int maxTries = 0;
        do
        {
            try
            {
                maxTries++;
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

