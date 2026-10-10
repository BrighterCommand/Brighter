using System;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;
using RabbitMQ.Client;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqSharedConnectionDroppedConfirmTests : IDisposable
{
    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private readonly RmqMessagingGatewayConnection _rmqConnection;
    private readonly CoalescingConfirmsRmqProducer _messageProducer;

    public RmqSharedConnectionDroppedConfirmTests()
    {
        _rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        _messageProducer = new CoalescingConfirmsRmqProducer(_rmqConnection);

        //we need a queue to avoid a discard
        new QueueFactory(_rmqConnection, new ChannelName(Guid.NewGuid().ToString()), new RoutingKeys(_routingKey))
            .Create(TimeSpan.FromMilliseconds(1000));
    }

    [Fact]
    public void When_the_shared_connection_is_dropped_should_select_confirms_on_the_new_channel()
    {
        //Arrange — after a send, another gateway drops the pooled connection this producer's channel is on
        _messageProducer.Send(NewMessage());

        new RmqMessageGatewayConnectionPool(_rmqConnection.Name!, _rmqConnection.Heartbeat)
            .RemoveConnection(new ConnectionFactory { Uri = _rmqConnection.AmpqUri!.Uri });

        //Act — the next send finds its channel closed, with no IOException, and opens a new one
        var error = Record.Exception(() => _messageProducer.Send(NewMessage()));

        //Assert — the send succeeds and the new channel is set up once
        Assert.Null(error);

        var newChannel = _messageProducer.Channels[1];
        Assert.Equal(1, newChannel.ConfirmSelectCount);
        Assert.Equal(1, newChannel.AckSubscriberCount);
        Assert.Equal(1, newChannel.NackSubscriberCount);
    }

    private Message NewMessage() => new(
        new MessageHeader(Id.Random(), _routingKey, MessageType.MT_COMMAND),
        new MessageBody("confirm me"));

    public void Dispose() => _messageProducer.Dispose();
}
