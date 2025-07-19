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

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Proactor;

[Trait("Category", "Kafka")]
[Trait("Fragile", "CI")]
[Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
public class KafkaMessageProducerMissingHeaderTestsAsync : IAsyncDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAMessageConsumerAsync _consumer;
    private readonly IProducer<string, byte[]> _producer;

    public KafkaMessageProducerMissingHeaderTestsAsync(ITestOutputHelper output)
    {
        const string groupId = "Kafka Message Producer Missing Header Test";
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
            QueueBufferingMaxKbytes = 1048576,
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
            .CreateAsync(new KafkaSubscription<MyCommand>(
                    channelName: new ChannelName(_queueName),
                    routingKey: new RoutingKey(_topic),
                    groupId: groupId,
                    messagePumpType: MessagePumpType.Proactor,
                    numOfPartitions: 1, replicationFactor: 1, makeChannels: OnMissingChannel.Create));
    }

    //[Fact(Skip = "As it has to wait for the messages to flush, only tends to run well in debug")]
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

        await _producer.ProduceAsync(_topic, kafkaMessage);
        
        //We should not need to flush, as the async does not queue work  - but in case this changes
        _producer.Flush();

        //let the message propagate on the broker
        await Task.Delay(3000);
        
        var receivedMessage = await GetMessageAsync();

        //Where we lack a partition key header, assume non-Brighter header and set to message key
        Assert.Equal(kafkaMessage.Key, receivedMessage.Header.PartitionKey);
        Assert.Equal(value, receivedMessage.Body.Bytes);
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
                _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
            }
        } while (maxTries <= 3);

        if (messages[0].Header.MessageType == MessageType.MT_NONE)
            throw new Exception($"Failed to read from topic:{_topic} after {maxTries} attempts");

        return messages[0];
    }
    
    public void Dispose()
    {
        _producer?.Dispose();
        ((IAmAMessageConsumerSync)_consumer)?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _producer?.Dispose();
        await _consumer.DisposeAsync();
    }
}
