using System;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using RabbitMQ.Client.Exceptions;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Proactor;

[Collection("RmqQuorumProactor")]
public class RmqQuorumProactorTests : RmqProactorTests
{
    protected override bool IsDurable => true;
    protected override QueueType QueueType => QueueType.Quorum;
    
    [Fact]
    public void When_creating_quorum_consumer_without_durability_should_throw()
    {
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.durableexchange", durable: true)
        };

        var queueName = new ChannelName(Guid.NewGuid().ToString());
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());

        var exception = Assert.Throws<ConfigurationException>(() =>
            new RmqMessageConsumer(rmqConnection, queueName, routingKey,
                isDurable: false, // This should cause the exception
                highAvailability: false,
                queueType: QueueType.Quorum));

        Assert.Contains("Quorum queues require durability to be enabled", exception.Message);
    }

    [Fact]
    public override Task When_rejecting_to_dead_letter_queue_a_message_due_to_queue_length_should_move_to_dlq()
    {
        // Quorum queue doesn't support this feature
        return Task.CompletedTask;
    }
    
    [Fact]
    public override async Task When_rejecting_a_message_due_to_queue_length_should_throw_publish_exception()
    {
        // arrange
        MaxQueueLength = 1;
        BufferSize = 1;
        
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Channel = await CreateChannelAsync(Subscription);
        Producer = await CreateProducerAsync(Publication);
        
        await Producer.SendAsync(CreateMessage(Publication.Topic!));
        await Producer.SendAsync(CreateMessage(Publication.Topic!));
        
        try
        {
            await Producer.SendAsync(CreateMessage(Publication.Topic!));
            await Producer.SendAsync(CreateMessage(Publication.Topic!));
            Assert.Fail("Exception an exception during publication");
        }
        catch (Exception e) when (e is not Xunit.Sdk.FailException)
        {
            Assert.IsType<PublishException>(e);
        }
        
        // Act
        var received = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
        await Channel.AcknowledgeAsync(received);
        
        received = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
        await Channel.AcknowledgeAsync(received);
        
        received = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
        await Channel.AcknowledgeAsync(received);
        
        received = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.Equal(MessageType.MT_NONE,  received.Header.MessageType);
    }
}
