using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Confluent.Kafka;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;
using Acks = Confluent.Kafka.Acks;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")]   //
public class KafkaMessageProducerMissingHeaderTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAMessageConsumerSync _consumer;
    private readonly IProducer<string,byte[]> _producer;

    public KafkaMessageProducerMissingHeaderTests(ITestOutputHelper output)
    {
        string groupId = Guid.NewGuid().ToString();
        _output = output;
        
        
        var clientConfig = new ClientConfig
        {
            Acks = (Acks)((int)Acks.All),
            BootstrapServers = string.Join(",", new[] { "localhost:9092" }),
            ClientId = "Kafka Producer Send with Missing Header Tests", 
        };

        var producerConfig = new ProducerConfig(clientConfig)
        {
            BatchNumMessages = 10, 
            EnableIdempotence = true,
            MaxInFlight = 1,
            LingerMs = 5,
            MessageTimeoutMs = 5000,
            MessageSendMaxRetries = 3,
            Partitioner = global::Confluent.Kafka.Partitioner.ConsistentRandom,
            QueueBufferingMaxMessages = 10,
            QueueBufferingMaxKbytes =  1048576,
            RequestTimeoutMs = 500,
            RetryBackoffMs = 100,
        };
        
        _producer = new ProducerBuilder<string, byte[]>(producerConfig)
            .SetErrorHandler((_, error) =>
            {
                output.WriteLine($"Kafka producer failed with Code: {error.Code}, Reason: { error.Reason}, Fatal: {error.IsFatal}", error.Code, error.Reason, error.IsFatal);
            })
            .Build();

        _consumer = new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer Test", BootStrapServers = new[] { "localhost:9092" }
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
    }

    [Fact]
    public async Task When_recieving_a_message_without_partition_key_header()
    {
        await Task.Delay(500); //Let topic propagate in the broker
        
        var command = new MyCommand { Value = "Test Content" };

        //vanilla i.e. no Kafka specific bytes at the beginning
        var body = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);
        var value = Encoding.UTF8.GetBytes(body);
        var kafkaMessage = new Message<string, byte[]>
        {
            Key = command.Id, 
            Value = value
        };

       _producer.Produce(_topic, kafkaMessage, report => _output.WriteLine(report.ToString()) );
       
       //ensure any messages are flushed
       _producer.Flush();

       //let this propogate to the Broker
       await Task.Delay(3000);

        var receivedMessage = GetMessage();

        //Where we lack a partition key header, assume non-Brighter header and set to message key
        Assert.Equal(command.Id, receivedMessage.Header.PartitionKey);
        Assert.Equal(value, receivedMessage.Body.Bytes);
    }

    private Message GetMessage()
    {
        Message[] messages = new Message[0];
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

                //wait before retry - allow consumer group join to complete
                Task.Delay(1000).GetAwaiter().GetResult();
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
        _producer?.Dispose();
        _consumer?.Dispose();
    }
}
