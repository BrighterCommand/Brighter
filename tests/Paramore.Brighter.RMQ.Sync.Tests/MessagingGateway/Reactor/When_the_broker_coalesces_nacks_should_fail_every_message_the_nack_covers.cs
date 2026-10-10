using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqCoalescedNackConfirmationTests : IDisposable
{
    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private readonly CoalescingConfirmsRmqProducer _messageProducer;

    public RmqCoalescedNackConfirmationTests()
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
    public void When_the_broker_coalesces_nacks_should_fail_every_message_the_nack_covers()
    {
        //Arrange
        var confirmations = new List<PublishConfirmationResult>();
        _messageProducer.OnMessagePublished += confirmations.Add;

        var sent = Enumerable.Range(0, 3)
            .Select(_ => new Message(
                new MessageHeader(Id.Random(), _routingKey, MessageType.MT_COMMAND),
                new MessageBody("reject me")))
            .ToArray();

        foreach (var message in sent)
            _messageProducer.Send(message);

        //Act — one frame nacks delivery tags 1 to 3
        _messageProducer.Confirms.RaiseNack(deliveryTag: 3, multiple: true);

        //Assert — each covered message is reported as failed once, in send order
        Assert.Equal(sent.Select(m => m.Id), confirmations.Select(c => c.MessageId));
        Assert.All(confirmations, c => Assert.False(c.Success));
    }

    public void Dispose() => _messageProducer.Dispose();
}
