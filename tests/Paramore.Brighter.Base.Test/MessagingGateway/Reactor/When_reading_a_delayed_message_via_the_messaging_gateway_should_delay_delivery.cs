using System.Threading;
using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Reactor;

public abstract partial class MessagingGatewayReactorTests<TPublication, TSubscription> : IAsyncLifetime
{
    
    [Fact]
    public void When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery()
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
        
        // Act
        var received = ReceiveMessage();
        Assert.Equal(MessageType.MT_NONE,  received.Header.MessageType);
        
        Thread.Sleep(MessageDelay);
        
        // Assert
        received = ReceiveMessage();
        AssertMessageAreEquals(message, received);
    }
}
