using System;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqChannelResetConfirmHandlerTests : IDisposable
{
    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private readonly CoalescingConfirmsRmqProducer _messageProducer;

    public RmqChannelResetConfirmHandlerTests()
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
    public void When_the_channel_resets_should_move_confirm_handlers_to_the_new_channel()
    {
        //Arrange — a send on the old channel, then its socket fails on the next send
        _messageProducer.Send(NewMessage());

        _messageProducer.Confirms.FailNextPublish();
        Assert.Throws<ChannelFailureException>(() => _messageProducer.Send(NewMessage()));

        //Act — the next send opens a new channel
        _messageProducer.Send(NewMessage());

        //Assert — the failed channel no longer calls the producer, and the new one is set up once
        var oldChannel = _messageProducer.Channels[0];
        var newChannel = _messageProducer.Channels[1];

        Assert.Equal(0, oldChannel.AckSubscriberCount);
        Assert.Equal(0, oldChannel.NackSubscriberCount);

        Assert.Equal(1, newChannel.ConfirmSelectCount);
        Assert.Equal(1, newChannel.AckSubscriberCount);
        Assert.Equal(1, newChannel.NackSubscriberCount);
    }

    private Message NewMessage() => new(
        new MessageHeader(Id.Random(), _routingKey, MessageType.MT_COMMAND),
        new MessageBody("confirm me"));

    public void Dispose() => _messageProducer.Dispose();
}
