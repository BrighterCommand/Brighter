using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqScheduledMessageConfirmationTests : IDisposable
{
    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private readonly SpyMessageSchedulerSync _scheduler = new();
    private readonly CoalescingConfirmsRmqProducer _messageProducer;

    public RmqScheduledMessageConfirmationTests()
    {
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        _messageProducer = new CoalescingConfirmsRmqProducer(rmqConnection) { Scheduler = _scheduler };

        new QueueFactory(rmqConnection, new ChannelName(Guid.NewGuid().ToString()), new RoutingKeys(_routingKey))
            .Create(TimeSpan.FromMilliseconds(1000));
    }

    [Fact]
    public void When_a_message_is_handed_to_the_scheduler_should_not_confirm_it_for_the_next_publish()
    {
        //Arrange
        var confirmations = new List<PublishConfirmationResult>();
        _messageProducer.OnMessagePublished += confirmations.Add;

        var scheduled = NewMessage();
        _messageProducer.SendWithDelay(scheduled, TimeSpan.FromSeconds(5));
        Assert.Equal([scheduled.Id], _scheduler.Scheduled.Select(m => m.Id));

        var published = NewMessage();
        _messageProducer.Send(published);

        //Act
        _messageProducer.Confirms.RaiseAck(deliveryTag: 1, multiple: false);

        //Assert
        Assert.Equal([published.Id], confirmations.Select(c => c.MessageId));
    }

    private Message NewMessage() => new(
        new MessageHeader(Id.Random(), _routingKey, MessageType.MT_COMMAND),
        new MessageBody("confirm me"));

    public void Dispose() => _messageProducer.Dispose();
}
