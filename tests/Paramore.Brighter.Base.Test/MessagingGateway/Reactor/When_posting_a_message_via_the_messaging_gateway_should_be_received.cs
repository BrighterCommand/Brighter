using System.Threading;
using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Reactor;

public abstract partial class MessagingGatewayReactorTests<TPublication, TSubscription> : IAsyncLifetime
{
    [Fact]
    public void When_posting_a_message_via_the_messaging_gateway_should_be_received()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = CreateProducer(Publication);
        Channel = CreateChannel(Subscription);
        
        var message = CreateMessage(Publication.Topic!);
        
        // Act
        Producer.Send(message);
        Thread.Sleep(DelayForReceiveMessage);
        var received = ReceiveMessage();
        
        // Assert
        AssertMessageAreEquals(message, received);
    }
}
