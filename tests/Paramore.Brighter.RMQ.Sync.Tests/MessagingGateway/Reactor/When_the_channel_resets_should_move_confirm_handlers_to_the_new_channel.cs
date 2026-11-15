using System;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;
using System.Threading.Tasks;
namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;

[Property("Category", "RMQ")]
[NotInParallel("RMQ")]
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

    [Test]
    public async Task When_the_channel_resets_should_move_confirm_handlers_to_the_new_channel()
    {
        //Arrange — a send on the old channel, then its socket fails on the next send
        _messageProducer.Send(NewMessage());

        _messageProducer.Confirms.FailNextPublish();
        await Assert.That(() => _messageProducer.Send(NewMessage())).ThrowsExactly<ChannelFailureException>();

        //Act — the next send opens a new channel
        _messageProducer.Send(NewMessage());

        //Assert — the failed channel no longer calls the producer, and the new one is set up once
        var oldChannel = _messageProducer.Channels[0];
        var newChannel = _messageProducer.Channels[1];

        await Assert.That(oldChannel.AckSubscriberCount).IsEqualTo(0);
        await Assert.That(oldChannel.NackSubscriberCount).IsEqualTo(0);

        await Assert.That(newChannel.ConfirmSelectCount).IsEqualTo(1);
        await Assert.That(newChannel.AckSubscriberCount).IsEqualTo(1);
        await Assert.That(newChannel.NackSubscriberCount).IsEqualTo(1);
    }

    private Message NewMessage() => new(
        new MessageHeader(Id.Random(), _routingKey, MessageType.MT_COMMAND),
        new MessageBody("confirm me"));

    public void Dispose() => _messageProducer.Dispose();
}
