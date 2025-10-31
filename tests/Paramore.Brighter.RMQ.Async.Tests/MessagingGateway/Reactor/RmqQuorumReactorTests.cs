using System;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using RabbitMQ.Client.Exceptions;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Reactor;

public class RmqQuorumReactorTests : RmqReactorTests
{
    protected override bool IsDurable => true;
    protected override QueueType QueueType => QueueType.Quorum;
    
    [Fact]
    public override void When_rejecting_to_dead_letter_queue_a_message_due_to_queue_length_should_move_to_dlq()
    {
        // Quorum queue doesn't support this feature
    }
    
    [Fact]
    public override void When_rejecting_a_message_due_to_queue_length_should_throw_publish_exception()
    {
        // arrange
        MaxQueueLength = 1;
        BufferSize = 1;
        
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Channel = CreateChannel(Subscription);
        Producer = CreateProducer(Publication);
        
        Producer.Send(CreateMessage(Publication.Topic!));
        Producer.Send(CreateMessage(Publication.Topic!));
        
        try
        {
            Producer.Send(CreateMessage(Publication.Topic!));
            Producer.Send(CreateMessage(Publication.Topic!));
            Assert.Fail("Exception an exception during publication");
        }
        catch (Exception e) when (e is not Xunit.Sdk.FailException)
        {
            Assert.IsType<PublishException>(e);
        }
        
        // Act
        var received = Channel.Receive(ReceiveTimeout);
        Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
        Channel.Acknowledge(received);
        
        received = Channel.Receive(ReceiveTimeout);
        Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
        Channel.Acknowledge(received);
        
        received = Channel.Receive(ReceiveTimeout);
        Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
        Channel.Acknowledge(received);
        
        received = Channel.Receive(ReceiveTimeout);
        Assert.Equal(MessageType.MT_NONE,  received.Header.MessageType);
    }
}
