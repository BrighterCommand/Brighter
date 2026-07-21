using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.Postgres;
using Paramore.Brighter.PostgresSQL.Tests.TestDoubles;

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway;

[Category("PostgresSql")]
public class PurgeTestAsync : IAsyncDisposable
{
    private readonly string _queueName = Guid.NewGuid().ToString();
    private IAmAProducerRegistry _producerRegistry;
    private IAmAMessageConsumerAsync _consumer;
    private RoutingKey _routingKey;

    [Before(Test)]
    public async Task Setup()
    {
        var testHelper = new PostgresSqlTestHelper();
        testHelper.SetupDatabase();

        _routingKey = new RoutingKey(Guid.NewGuid().ToString());

        var sub = new PostgresSubscription<MyCommand>(
            new SubscriptionName(_queueName),
            new ChannelName(_routingKey.Value), _routingKey,
            messagePumpType: MessagePumpType.Proactor);

        _producerRegistry = await new PostgresProducerRegistryFactory(
                new PostgresMessagingGatewayConnection(testHelper.Configuration),
            [new PostgresPublication { Topic = _routingKey } ]
        ).CreateAsync();

        _consumer = new PostgresConsumerFactory(new PostgresMessagingGatewayConnection(testHelper.Configuration)).CreateAsync(sub);
    }

    [Test]
    public async Task When_queue_is_Purged()
    {
        //Send a sequence of messages to postgres 
        var msgId = await SendMessageAsync();

        // Now read those messages in order
        var firstMessage = await ConsumeMessagesAsync(_consumer);
        var message = firstMessage.First();
        await Assert.That(message.Id).IsEqualTo(msgId);

        await _consumer.PurgeAsync();

        // Next Message should not exist (default will be returned)
        var nextMessage = await ConsumeMessagesAsync(_consumer);
        message = nextMessage.First();

        await Assert.That(message).IsEqualTo(new Message());
    }

    private async Task<string> SendMessageAsync()
    {
        var messageId = Guid.NewGuid().ToString();

        await _producerRegistry.LookupAsyncBy(_routingKey).SendAsync(new Message(
            new MessageHeader(messageId, _routingKey, MessageType.MT_COMMAND),
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
                // Lots of reasons to be here as Kafka propagates a topic, or the test cluster is still initializing
                //_output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
            }
        } while (maxTries <= 3);

        return messages;
    }

    [After(Test)]
    public async Task Cleanup()
    {
        ((IAmAMessageConsumerSync)_consumer).Dispose();
        _producerRegistry.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _consumer.DisposeAsync();
        _producerRegistry.Dispose();
    }
}
