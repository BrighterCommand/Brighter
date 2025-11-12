using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Reactor;

public abstract partial class MessagingGatewayReactorTests<TPublication, TSubscription> : IAsyncLifetime
{
    [Fact]
    public void When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue()
    {
        if (!HasSupportToMoveToDeadLetterQueueAfterTooManyRetries)
        {
            return;
        }
        
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName(), setupDeadLetterQueue: true);
        Producer = CreateProducer(Publication);
        Channel = CreateChannel(Subscription);
        
        var message = CreateMessage(Publication.Topic!);
        Producer.Send(message);

        Message? received;
        for (var i = 0; i < Subscription.RequeueCount; i++)
        {
            received = ReceiveMessage();
            Channel.Requeue(received);
        }
        
        received = ReceiveMessage();
        Assert.Equal(MessageType.MT_NONE, received.Header.MessageType);

        received = GetMessageFromDeadLetterQueue(Subscription);
        
        // Assert
        AssertMessageAreEquals(message, received); 
    }
}
