using System.Threading;
using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Reactor;

public abstract partial class MessagingGatewayReactorTests<TPublication, TSubscription> : IAsyncLifetime
{
    [Fact]
    public void When_requeing_a_failed_message_with_delay_should_receive_message_again()
    {
        if (!HasSupportToDelayedMessages)
        {
            return;
        }
        
        // arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = CreateProducer(Publication);
        Channel = CreateChannel(Subscription);

        var message = CreateMessage(Publication.Topic!);
        Producer.SendWithDelay(message, MessageDelay);
        
        Thread.Sleep(MessageDelay);
        
        // Act
        var received = ReceiveMessage();
        Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
        
        Assert.True(Channel.Requeue(received));
        Thread.Sleep(DelayForRequeueMessage);
        
        // Assert
        received = ReceiveMessage();
        Channel.Acknowledge(received);
        AssertMessageAreEquals(message, received);
    }   
}
