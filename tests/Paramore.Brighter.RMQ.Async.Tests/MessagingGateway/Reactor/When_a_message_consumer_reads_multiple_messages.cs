using System;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RMQBufferedConsumerTests : IDisposable
{
    private readonly IAmAMessageProducerSync _messageProducer;
    private readonly IAmAMessageConsumerSync _messageConsumer;
    private readonly ChannelName _channelName = new(Guid.NewGuid().ToString());
    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private const int BatchSize = 3;

    public RMQBufferedConsumerTests()
    {
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        _messageProducer = new RmqMessageProducer(rmqConnection);
        _messageConsumer = new RmqMessageConsumer(connection:rmqConnection, queueName:_channelName, routingKey:_routingKey, isDurable:false, highAvailability:false, batchSize:BatchSize);
            
        //create the queue, so that we can receive messages posted to it
        new QueueFactory(rmqConnection, _channelName, new RoutingKeys(_routingKey)).CreateAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public void When_a_message_consumer_reads_multiple_messages()
    {
        //Post one more than batch size messages
        var messageOne = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content One"));
        _messageProducer.Send(messageOne);
        var messageTwo= new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content Two"));
        _messageProducer.Send(messageTwo);
        var messageThree= new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content Three"));
        _messageProducer.Send(messageThree);
        var messageFour= new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND), new MessageBody("test content Four"));
        _messageProducer.Send(messageFour);
            
        //let them arrive
        Task.Delay(5000);
            
        //Now retrieve messages from the consumer
        var messages = _messageConsumer.Receive(TimeSpan.FromMilliseconds(1000));
            
        //We should only have three messages
        Assert.Equal(3, messages.Length);
            
        //ack those to remove from the queue
        foreach (var message in messages)
        {
            _messageConsumer.Acknowledge(message);
        }

        //Allow ack to register
        Task.Delay(1000);
            
        //Now retrieve again
        messages = _messageConsumer.Receive(TimeSpan.FromMilliseconds(500));

        //This time, just the one message
        Assert.Equal(1, messages.Length);

    }

    public void Dispose()
    {
        _messageConsumer.Purge();
        _messageConsumer.Dispose();
        _messageProducer.Dispose();
    }
}
