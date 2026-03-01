using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
public class KafkaMessageConsumerPreservesOrder : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly RoutingKey _topic = new(Guid.NewGuid().ToString());
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly string _partitionKey = Guid.NewGuid().ToString();
    private readonly string _kafkaGroupId = Guid.NewGuid().ToString();


    public KafkaMessageConsumerPreservesOrder (ITestOutputHelper output)
    {
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
    }

    [Fact]
    public async Task When_a_message_is_sent_keep_order()
    {
        //Let topic propogate
        await Task.Delay(500);
         
        IAmAMessageConsumerSync consumer = null;
        try
        {
            //Send a sequence of messages to Kafka
            var routingKey = new RoutingKey(_topic);
            var producer = ((IAmAMessageProducerSync)_producerRegistry.LookupBy(routingKey));
            var msgId = SendMessage(producer);
            var msgId2 = SendMessage(producer);
            var msgId3 = SendMessage(producer);
            var msgId4 = SendMessage(producer);
            
            //ensure the messages are sent
            ((KafkaMessageProducer)producer).Flush();
                  
            consumer = CreateConsumer();
            
            //Now read messages in order
            var firstMessage = ConsumeMessages(consumer);
            var message = firstMessage.First();
            Assert.Equal(msgId, message.Id);
            consumer.Acknowledge(message);

            var secondMessage = ConsumeMessages(consumer);
            message = secondMessage.First();
            Assert.Equal(msgId2, message.Id);
            consumer.Acknowledge(message);

            var thirdMessages = ConsumeMessages(consumer);
            message = thirdMessages.First();
            Assert.Equal(msgId3, message.Id);
            consumer.Acknowledge(message);

            var fourthMessage = ConsumeMessages(consumer);
            message = fourthMessage.First();
            Assert.Equal(msgId4, message.Id);
            consumer.Acknowledge(message);
 
        }
        finally
        {
            consumer?.Dispose();
        }
    }

    private string SendMessage(IAmAMessageProducerSync producer)
    {
        var messageId = Guid.NewGuid().ToString();

        producer.Send(
            new Message(
                new MessageHeader(messageId, _topic, MessageType.MT_COMMAND)
                {
                    PartitionKey = _partitionKey
                },
                new MessageBody($"test content [{_queueName}]")
            )
        );

        return messageId;
    }

    private IEnumerable<Message> ConsumeMessages(IAmAMessageConsumerSync consumer)
    {
        var messages = Array.Empty<Message>();
        int maxTries = 0;
        do
        {
            try
            {
                maxTries++;
                messages = consumer.Receive(TimeSpan.FromMilliseconds(1000));

                if (messages[0].Header.MessageType != MessageType.MT_NONE)
                    break;
            }
            catch (ChannelFailureException cfx)
            {
                //Lots of reasons to be here as Kafka propagates a topic, or the test cluster is still initializing
                _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                Task.Delay(1000).GetAwaiter().GetResult();
            }
        } while (maxTries <= 10);

        return messages;
    }

    private IAmAMessageConsumerSync CreateConsumer()
    {
        return new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer Test",
                    BootStrapServers = ["localhost:9092"]
                })
            .Create(new KafkaSubscription<MyCommand>(
                subscriptionName: new SubscriptionName("Paramore.Brighter.Tests"),
                channelName: new ChannelName(_queueName),
                routingKey: new RoutingKey(_topic),
                groupId: _kafkaGroupId,
                offsetDefault: AutoOffsetReset.Earliest,
                commitBatchSize:1,
                numOfPartitions: 1,
                replicationFactor: 1,
                messagePumpType: MessagePumpType.Reactor,
                makeChannels: OnMissingChannel.Create
            ));
    }

    public void Dispose()
    {
        _producerRegistry?.Dispose();
    }
}
