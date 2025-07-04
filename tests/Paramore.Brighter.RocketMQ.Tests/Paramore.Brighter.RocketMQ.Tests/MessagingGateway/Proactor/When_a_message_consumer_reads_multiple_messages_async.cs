using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Proactor;

[Trait("Category", "RocketMQ")]
public class BufferedConsumerTestsAsync : IAsyncDisposable
{
    private readonly IAmAMessageProducerAsync _messageProducer;
    private readonly IAmAMessageConsumerAsync _messageConsumer;
    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private const int BatchSize = 3;

    public BufferedConsumerTestsAsync()
    {
        var connection = GatewayFactory.CreateConnection(); 
        var publication = new RocketMqPublication { Topic = "bt_mc_rmm_async" };
        var consumer = GatewayFactory.CreateSimpleConsumer(connection, publication).GetAwaiter().GetResult();
        var producer = GatewayFactory.CreateProducer(connection,  publication).GetAwaiter().GetResult();

        _messageConsumer  = new RocketMessageConsumer(consumer, BatchSize, TimeSpan.FromSeconds(30));
        _messageProducer = new RocketMqMessageProducer(connection, producer, publication);
    }

    [Fact]
    public async Task When_a_message_consumer_reads_multiple_messages_async()
    {
        await _messageConsumer.PurgeAsync();
        
        //Post one more than batch size messages
        var messageOne = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content One"));
        await _messageProducer.SendAsync(messageOne);
        var messageTwo = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content Two"));
        await _messageProducer.SendAsync(messageTwo);
        var messageThree = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content Three"));
        await _messageProducer.SendAsync(messageThree);
        var messageFour = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content Four"));
        await _messageProducer.SendAsync(messageFour);

        //let them arrive
        await Task.Delay(5000);

        //Now retrieve messages from the consumer
        var messages = await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));

        //We should only have three messages
        Assert.Equal(3, messages.Length);

        //ack those to remove from the queue
        foreach (var message in messages)
        {
            await _messageConsumer.AcknowledgeAsync(message);
        }

        //Allow ack to register
        await Task.Delay(1000);

        //Now retrieve again
        messages = await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(500));

        //This time, just the one message
        Assert.Single(messages);
    }

    public async ValueTask DisposeAsync()
    {
        await _messageConsumer.PurgeAsync();
        await _messageProducer.DisposeAsync();
        await _messageConsumer.DisposeAsync();
    }
}
