using System;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqConfirmSelectOncePerChannelTests : IDisposable
{
    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private readonly CoalescingConfirmsRmqProducer _messageProducer;

    public RmqConfirmSelectOncePerChannelTests()
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
    public void When_sending_several_messages_on_one_channel_should_select_confirms_once()
    {
        //Arrange — the first send opens the channel
        _messageProducer.Send(NewMessage());

        //Act — further sends reuse the same channel
        _messageProducer.Send(NewMessage());
        _messageProducer.Send(NewMessage());

        //Assert — the channel was put into confirm mode, and given its confirm handlers, only once
        Assert.Equal(1, _messageProducer.Confirms.ConfirmSelectCount);
        Assert.Equal(1, _messageProducer.Confirms.AckSubscriberCount);
        Assert.Equal(1, _messageProducer.Confirms.NackSubscriberCount);
    }

    private Message NewMessage() => new(
        new MessageHeader(Id.Random(), _routingKey, MessageType.MT_COMMAND),
        new MessageBody("confirm me"));

    public void Dispose() => _messageProducer.Dispose();
}
