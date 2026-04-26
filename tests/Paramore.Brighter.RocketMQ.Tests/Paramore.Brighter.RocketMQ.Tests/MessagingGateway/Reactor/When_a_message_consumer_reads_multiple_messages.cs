using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.Utils;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Reactor;

[Category("RocketMQ")]
public class BufferedConsumerTests : IDisposable
{
    private IAmAMessageProducerSync _messageProducer;
    private IAmAMessageConsumerSync _messageConsumer;
    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private const int BatchSize = 3;

    [Before(Test)]
    public async Task Setup()
    {
        var connection = GatewayFactory.CreateConnection();
        var publication = new RocketMqPublication { Topic = "bt_mc_rmm" };
        var consumer = await GatewayFactory.CreateSimpleConsumer(connection, publication);
        var producer = await GatewayFactory.CreateProducer(connection,  publication);

        _messageConsumer  = new RocketMessageConsumer(consumer, BatchSize, TimeSpan.FromSeconds(30));
        _messageProducer = new RocketMqMessageProducer(connection, producer, publication);
    }

    [Test]
    public async Task When_a_message_consumer_reads_multiple_messages()
    {
        _messageConsumer.Purge();
        //Post one more than batch size messages
        var messageOne = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content One"));
        _messageProducer.Send(messageOne);
        var messageTwo = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content Two"));
        _messageProducer.Send(messageTwo);
        var messageThree = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content Three"));
        _messageProducer.Send(messageThree);
        var messageFour = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content Four"));
        _messageProducer.Send(messageFour);

        //let them arrive
        Thread.Sleep(5000);

        //Now retrieve messages from the consumer
        var messages = _messageConsumer.Receive(TimeSpan.FromMilliseconds(1000));

        //We should only have three messages
        await Assert.That(messages.Length).IsEqualTo(3);

        //ack those to remove from the queue
        foreach (var message in messages)
        {
            _messageConsumer.Acknowledge(message);
        }

        //Allow ack to register
        Thread.Sleep(1000);

        //Now retrieve again
        messages = _messageConsumer.Receive(TimeSpan.FromMilliseconds(500));

        //This time, just the one message
        await Assert.That(messages).HasSingleItem();
    }

    public void Dispose()
    {
        _messageConsumer.Purge();
        _messageProducer.Dispose();
        _messageConsumer.Dispose();
    }
}
