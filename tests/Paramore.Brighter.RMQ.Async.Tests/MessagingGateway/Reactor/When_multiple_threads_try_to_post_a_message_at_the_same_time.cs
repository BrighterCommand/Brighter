using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqMessageProducerSupportsMultipleThreadsTests : IDisposable
{
    private readonly IAmAMessageProducerSync _messageProducer;
    private readonly Message _message;

    public RmqMessageProducerSupportsMultipleThreadsTests()
    {
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("nonexistenttopic"), 
                MessageType.MT_COMMAND), 
            new MessageBody("test content"));

        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        _messageProducer = new RmqMessageProducer(rmqConnection);
    }

    [Fact]
    public void When_multiple_threads_try_to_post_a_message_at_the_same_time()
    {
        bool exceptionHappened = false;
        try
        {
            Parallel.ForEach(Enumerable.Range(0, 10), _ =>
            {
                _messageProducer.Send(_message);
            });
        }
        catch (Exception)
        {
            exceptionHappened = true;
        }

        //_should_not_throw
        Assert.False(exceptionHappened);
    }

    public void Dispose()
    {
        _messageProducer.Dispose();
    }
}
