using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqThrowingConfirmationSubscriberTests : IDisposable
{
    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private readonly CoalescingConfirmsRmqProducer _messageProducer;

    public RmqThrowingConfirmationSubscriberTests()
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
    public void When_a_confirmation_subscriber_throws_should_still_confirm_every_message_once()
    {
        //Arrange — the subscriber fails on the first confirmation it is given
        var confirmations = new List<PublishConfirmationResult>();
        _messageProducer.OnMessagePublished += result =>
        {
            confirmations.Add(result);
            if (confirmations.Count == 1) throw new InvalidOperationException("Subscriber failed");
        };

        var sent = Enumerable.Range(0, 3)
            .Select(_ => new Message(
                new MessageHeader(Id.Random(), _routingKey, MessageType.MT_COMMAND),
                new MessageBody("confirm me")))
            .ToArray();

        foreach (var message in sent)
            _messageProducer.Send(message);

        //Act — one frame acks delivery tags 1 to 3
        _messageProducer.Confirms.RaiseAck(deliveryTag: 3, multiple: true);

        //Assert — the failure loses no confirmation and repeats none
        Assert.Equal(sent.Select(m => m.Id), confirmations.Select(c => c.MessageId));
    }

    public void Dispose() => _messageProducer.Dispose();
}
