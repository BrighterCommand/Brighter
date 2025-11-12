using System.Threading;
using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Reactor;

public abstract partial class MessagingGatewayReactorTests<TPublication, TSubscription> : IAsyncLifetime
{
    [Fact]
    public virtual void When_requeing_a_failed_message_should_receive_message_again()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = CreateProducer(Publication);
        Channel = CreateChannel(Subscription);
        
        var message = CreateMessage(Publication.Topic!);
        Producer.Send(message);
        
        Thread.Sleep(DelayForReceiveMessage);
        
        // Act
        var received = ReceiveMessage();
        Assert.NotEqual(MessageType.MT_QUIT,  received.Header.MessageType);

        Assert.True(Channel.Requeue(received));

        Thread.Sleep(DelayForRequeueMessage);
        received = ReceiveMessage(true);
        
        // Assert
        AssertMessageAreEquals(message, received); 
    }   
}
