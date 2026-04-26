using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Category("Kafka")]
public class KafkaMessageConsumerPreservesOrder : IDisposable
{
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly RoutingKey _topic = new(Guid.NewGuid().ToString());
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly string _partitionKey = Guid.NewGuid().ToString();
    private readonly string _kafkaGroupId = Guid.NewGuid().ToString();


    public KafkaMessageConsumerPreservesOrder ()
    {
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

    [Test]
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
            var firstMessage = await ConsumeMessages(consumer);
            var message = firstMessage.First();
            await Assert.That(message.Id).IsEqualTo(msgId);
            consumer.Acknowledge(message);

            var secondMessage = await ConsumeMessages(consumer);
            message = secondMessage.First();
            await Assert.That(message.Id).IsEqualTo(msgId2);
            consumer.Acknowledge(message);

            var thirdMessages = await ConsumeMessages(consumer);
            message = thirdMessages.First();
            await Assert.That(message.Id).IsEqualTo(msgId3);
            consumer.Acknowledge(message);

            var fourthMessage = await ConsumeMessages(consumer);
            message = fourthMessage.First();
            await Assert.That(message.Id).IsEqualTo(msgId4);
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

    private async Task<IEnumerable<Message>> ConsumeMessages(IAmAMessageConsumerSync consumer)
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
                Console.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                await Task.Delay(1000);
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

