using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
public class KafkaMessageProducerHeaderBytesSendTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString(); 
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly IAmAMessageConsumerSync _consumer;
    private readonly string _partitionKey = Guid.NewGuid().ToString();
    private readonly ISerializer<MyKafkaCommand> _serializer;
    private readonly IDeserializer<MyKafkaCommand> _deserializer;
    private readonly SerializationContext _serializationContext;


    public KafkaMessageProducerHeaderBytesSendTests (ITestOutputHelper output)
    {
        string groupId = Guid.NewGuid().ToString();
        _output = output;
        _producerRegistry = new KafkaProducerRegistryFactory(
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
            ]).Create(); 
            
        _consumer = new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer Test",
                    BootStrapServers = ["localhost:9092"]
                })
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
            
        var schemaRegistryConfig = new SchemaRegistryConfig { Url = "http://localhost:8081"};
        ISchemaRegistryClient schemaRegistryClient = new CachedSchemaRegistryClient(schemaRegistryConfig);
  
        _serializer = new JsonSerializer<MyKafkaCommand>(schemaRegistryClient, ConfluentJsonSerializationConfig.SerdesJsonSerializerConfig(), ConfluentJsonSerializationConfig.NJsonSchemaGeneratorSettings()).AsSyncOverAsync();
        _deserializer = new JsonDeserializer<MyKafkaCommand>().AsSyncOverAsync();
        _serializationContext = new SerializationContext(MessageComponentType.Value, _topic);
    }

    /// <summary>
    /// NOTE: This test needs the schema registry to be running, and has hardcoded it's port to 8081. Both of those
    /// may cause this test to fail, so check them if in doubt
    /// </summary>
    [Fact]
    public async Task When_posting_a_message_via_the_messaging_gateway()
    {
        
        await Task.Delay(500); //Let topic propagate in the broker
        
        //arrange
            
        var myCommand = new MyKafkaCommand{ Value = "Hello World"};
            
        //use the serdes json serializer to write the message to the topic
        var body = _serializer.Serialize(myCommand, _serializationContext);
            
        //grab the schema id that was written to the message by the serializer
        var schemaId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(body.Skip(1).Take(4).ToArray()));

        var routingKey = new RoutingKey(_topic);
            
        var sent = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND)
            {
                PartitionKey = _partitionKey
            },
            new MessageBody(body));
            
        //act

        var producer = ((IAmAMessageProducerSync)_producerRegistry.LookupBy(routingKey));
        producer.Send(sent);
        
        //ensure that the messages are all sent
        ((KafkaMessageProducer) producer).Flush();
        
        await Task.Delay(500); //Let the message propagate in the broker

        var received = GetMessage();

        Assert.True(received.Body.Bytes.Length > 5);
            
        var receivedSchemaId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(received.Body.Bytes.Skip(1).Take(4).ToArray()));
            
        var receivedCommand = _deserializer.Deserialize(received.Body.Bytes, received.Body.Bytes is null, _serializationContext);
            
        //assert
        Assert.Equal(MessageType.MT_COMMAND, received.Header.MessageType);
        Assert.Equal(_partitionKey, received.Header.PartitionKey);
        Assert.Equal(received.Body.Bytes, received.Body.Bytes);
        Assert.Equal(received.Body.Value, received.Body.Value);
        Assert.Equal(schemaId, receivedSchemaId);
        Assert.Equal(myCommand.Id, receivedCommand.Id);
        Assert.Equal(myCommand.Value, receivedCommand.Value);
    }

    private Message GetMessage()
    {
        Message[] messages = Array.Empty<Message>();
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
                Task.Delay(1000).GetAwaiter().GetResult();
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
