using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.Postgres;
using Paramore.Brighter.PostgresSQL.Tests.TestDoubles;

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway;

[Category("PostgresSql")]
public class OrderTestAsync : IAsyncDisposable
{
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topicName = Guid.NewGuid().ToString();
    private IAmAProducerRegistry _producerRegistry;
    private IAmAMessageConsumerAsync _consumer;

    [Before(Test)]
    public async Task Setup()
    {
        var testHelper = new PostgresSqlTestHelper();
        testHelper.SetupDatabase();

        var routingKey = new RoutingKey(_topicName);

        var sub = new PostgresSubscription<MyCommand>(
            new SubscriptionName(_queueName),
            new ChannelName(_topicName), routingKey,
            messagePumpType: MessagePumpType.Proactor);

        _producerRegistry = await new PostgresProducerRegistryFactory(
            new PostgresMessagingGatewayConnection(testHelper.Configuration),
            [new PostgresPublication { Topic = routingKey }]
        ).CreateAsync();

        _consumer = new PostgresConsumerFactory(new PostgresMessagingGatewayConnection(testHelper.Configuration)).CreateAsync(sub);
    }

    [Test]
    public async Task When_a_message_is_sent_keep_order()
    {
        //Send a sequence of messages to postgres 
        var msgId = await SendMessageAsync();
        var msgId2 = await SendMessageAsync();
        var msgId3 = await SendMessageAsync();
        var msgId4 = await SendMessageAsync();

        //Now read those messages in order

        var firstMessage = await ConsumeMessagesAsync(_consumer);
        var message = firstMessage.First();
        await Assert.That(message.IsEmpty).IsFalse();
        await Assert.That(message.Id).IsEqualTo(msgId);

        var secondMessage = await ConsumeMessagesAsync(_consumer);
        message = secondMessage.First();
        await Assert.That(message.IsEmpty).IsFalse();
        await Assert.That(message.Id).IsEqualTo(msgId2);

        var thirdMessages = await ConsumeMessagesAsync(_consumer);
        message = thirdMessages.First();
        await Assert.That(message.IsEmpty).IsFalse();
        await Assert.That(message.Id).IsEqualTo(msgId3);

        var fourthMessage = await ConsumeMessagesAsync(_consumer);
        message = fourthMessage.First();
        await Assert.That(message.IsEmpty).IsFalse();
        await Assert.That(message.Id).IsEqualTo(msgId4);
    }

    private async Task<string> SendMessageAsync()
    {
        var messageId = Guid.NewGuid().ToString();

        var routingKey = new RoutingKey(_topicName);
        await _producerRegistry.LookupAsyncBy(routingKey).SendAsync(new Message(
            new MessageHeader(messageId, routingKey, MessageType.MT_COMMAND),
            new MessageBody($"test content [{_queueName}]")));

        return messageId;
    }

    private async Task<IEnumerable<Message>> ConsumeMessagesAsync(IAmAMessageConsumerAsync consumer)
    {
        var messages = Array.Empty<Message>();
        int maxTries = 0;
        do
        {
            try
            {
                maxTries++;
                messages = await consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));

                if (messages[0].Header.MessageType != MessageType.MT_NONE)
                    break;
            }
            catch (ChannelFailureException)
            {
                //Lots of reasons to be here as Kafka propagates a topic, or the test cluster is still initializing
                //_output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
            }
        } while (maxTries <= 3);

        return messages;
    }

    [After(Test)]
    public async Task Cleanup()
    {
        _producerRegistry.Dispose();
        ((IAmAMessageConsumerSync)_consumer).Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _producerRegistry.Dispose();
        await _consumer.DisposeAsync();
    }
}
