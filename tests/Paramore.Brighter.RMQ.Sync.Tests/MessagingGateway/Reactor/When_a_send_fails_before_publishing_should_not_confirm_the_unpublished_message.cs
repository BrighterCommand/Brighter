using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqSendFailureConfirmationTests : IDisposable
{
    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private readonly CoalescingConfirmsRmqProducer _messageProducer;

    public RmqSendFailureConfirmationTests()
    {
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        _messageProducer = new CoalescingConfirmsRmqProducer(rmqConnection);

        //we need a queue to avoid a discard
        new QueueFactory(rmqConnection, new ChannelName(Guid.NewGuid().ToString()), new RoutingKeys(_routingKey))
            .Create(TimeSpan.FromMilliseconds(1000));
    }

    [Fact]
    public void When_a_send_fails_before_publishing_should_not_confirm_the_unpublished_message()
    {
        //Arrange — a Guid is not an AMQP header type, so Send throws before anything reaches the broker
        var confirmations = new List<PublishConfirmationResult>();
        _messageProducer.OnMessagePublished += confirmations.Add;

        var unpublishable = NewMessage();
        unpublishable.Header.Bag["not-an-amqp-type"] = Guid.NewGuid();
        Assert.Throws<ConfigurationException>(() => _messageProducer.Send(unpublishable));

        // the channel is still open and the failed Send never took a delivery tag, so the next message gets it
        var published = NewMessage();
        _messageProducer.Send(published);

        //Act — the broker acks the one message it received
        _messageProducer.Confirms.RaiseAck(deliveryTag: 1, multiple: false);

        //Assert — only the published message is confirmed
        Assert.Equal([published.Id], confirmations.Select(c => c.MessageId));
    }

    private Message NewMessage() => new(
        new MessageHeader(Id.Random(), _routingKey, MessageType.MT_COMMAND),
        new MessageBody("confirm me"));

    public void Dispose() => _messageProducer.Dispose();
}
