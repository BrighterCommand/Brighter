using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqChannelResetConfirmationTests : IDisposable
{
    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private readonly CoalescingConfirmsRmqProducer _messageProducer;

    public RmqChannelResetConfirmationTests()
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
    public void When_the_channel_resets_should_not_confirm_messages_pending_on_the_old_channel()
    {
        //Arrange — two messages await confirmation on the old channel when its socket fails
        var confirmations = new List<PublishConfirmationResult>();
        _messageProducer.OnMessagePublished += confirmations.Add;

        _messageProducer.Send(NewMessage());
        _messageProducer.Send(NewMessage());

        _messageProducer.Confirms.FailNextPublish();
        Assert.Throws<ChannelFailureException>(() => _messageProducer.Send(NewMessage()));

        // the new channel numbers its delivery tags from 1 again
        var sentOnNewChannel = new[] { NewMessage(), NewMessage() };
        foreach (var message in sentOnNewChannel)
            _messageProducer.Send(message);

        //Act — one frame on the new channel acks its delivery tags 1 to 2
        _messageProducer.Confirms.RaiseAck(deliveryTag: 2, multiple: true);

        //Assert — only the messages sent on the new channel are confirmed
        Assert.Equal(sentOnNewChannel.Select(m => m.Id), confirmations.Select(c => c.MessageId));
    }

    private Message NewMessage() => new(
        new MessageHeader(Id.Random(), _routingKey, MessageType.MT_COMMAND),
        new MessageBody("confirm me"));

    public void Dispose() => _messageProducer.Dispose();
}
