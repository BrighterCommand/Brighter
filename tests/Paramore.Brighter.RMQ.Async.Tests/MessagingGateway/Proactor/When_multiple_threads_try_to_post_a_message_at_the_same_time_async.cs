using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Proactor;

[Trait("Category", "RMQ")]
public class RmqMessageProducerSupportsMultipleThreadsTestsAsync : IDisposable, IAsyncDisposable
{
    private readonly IAmAMessageProducerAsync _messageProducer;
    private readonly Message _message;

    public RmqMessageProducerSupportsMultipleThreadsTestsAsync()
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
    public async Task When_multiple_threads_try_to_post_a_message_at_the_same_time()
    {
        bool exceptionHappened = false;
        try
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = 4 };
            await Parallel.ForEachAsync(Enumerable.Range(0, 10), options, async (_, ct) =>
            {
                await _messageProducer.SendAsync(_message, ct);
            });
        }
        catch (Exception)
        {
            exceptionHappened = true;
        }

        //_should_not_throw
        exceptionHappened.Should().BeFalse();
    }

    public void Dispose()
    {
        ((IAmAMessageProducerSync)_messageProducer).Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _messageProducer.DisposeAsync();
    }
}
