using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.RMQ;
using Xunit;

namespace Paramore.Brighter.RMQ.Tests.MessagingGateway;

[Trait("Category", "RMQ")]
public class RmqMessageProducerSendPersistentMessageTestsAsync : IDisposable, IAsyncDisposable
{
    private IAmAMessageProducerAsync _messageProducer;
    private IAmAMessageConsumerAsync _messageConsumer;
    private Message _message;

    public RmqMessageProducerSendPersistentMessageTestsAsync()
    {
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(Guid.NewGuid().ToString()), 
                MessageType.MT_COMMAND),
            new MessageBody("test content"));

        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange"),
            PersistMessages = true
        };

        _messageProducer = new RmqMessageProducer(rmqConnection);
        var queueName = new ChannelName(Guid.NewGuid().ToString());
            
        _messageConsumer = new RmqMessageConsumer(rmqConnection, queueName, _message.Header.Topic, false);

        new QueueFactory(rmqConnection, queueName, new RoutingKeys( _message.Header.Topic))
            .CreateAsync()
            .GetAwaiter()
            .GetResult();
    }

    [Fact]
    public async Task When_posting_a_message_to_persist_via_the_messaging_gateway()
    {
        // arrange
        await _messageProducer.SendAsync(_message);

        // act
        var result = (await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000))).First();

        // assert
        result.Persist.Should().Be(true);
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
