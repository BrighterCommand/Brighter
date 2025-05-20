using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.Postgres;
using Paramore.Brighter.PostgresSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway;

[Trait("Category", "PostgresSql")]
public class OrderTestAsync : IAsyncDisposable, IDisposable
{
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topicName = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly IAmAMessageConsumerAsync _consumer;

    public OrderTestAsync()
    {
        var testHelper = new PostgresSqlTestHelper();
        testHelper.SetupDatabase();

        var routingKey = new RoutingKey(_topicName);

        var sub = new PostgresSubscription<MyCommand>(
            new SubscriptionName(_queueName),
            new ChannelName(_topicName), routingKey,
            messagePumpType: MessagePumpType.Proactor);

        _producerRegistry = new PostgresProducerRegistryFactory(
            new PostgresMessagingGatewayConnection(testHelper.Configuration),
            [new PostgresPublication { Topic = routingKey }]
        ).CreateAsync().GetAwaiter().GetResult();
            
        _consumer = new PostgresConsumerFactory(new PostgresMessagingGatewayConnection(testHelper.Configuration)).CreateAsync(sub);
    }

    [Fact]
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
        Assert.False(message.IsEmpty);
        Assert.Equal(msgId, message.Id);

        var secondMessage = await ConsumeMessagesAsync(_consumer);
        message = secondMessage.First();
        Assert.False(message.IsEmpty);
        Assert.Equal(msgId2, message.Id);

        var thirdMessages = await ConsumeMessagesAsync(_consumer);
        message = thirdMessages.First();
        Assert.False(message.IsEmpty);
        Assert.Equal(msgId3, message.Id);

        var fourthMessage = await ConsumeMessagesAsync(_consumer);
        message = fourthMessage.First();
        Assert.False(message.IsEmpty);
        Assert.Equal(msgId4, message.Id);
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

    public void Dispose()
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
