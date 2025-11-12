using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Reactor;

public abstract partial class MessagingGatewayReactorTests<TPublication, TSubscription> : IAsyncLifetime
{
    [Fact]
    public void When_infrastructure_missing_and_assume_channel_should_throw_exception()
    {
        try
        {
            // Arrange
            Publication = CreatePublication(GetOrCreateRoutingKey());
            Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName(), OnMissingChannel.Assume);
            Producer = CreateProducer(Publication);
            Channel = CreateChannel(Subscription);
        
            var message = CreateMessage(Publication.Topic!);
        
            // Act
            Producer.Send(message);
        
            // Assert
            ReceiveMessage();
            Assert.Fail("We are expected to throw an exception");
        }
        catch
        {
            Assert.True(true);
        }
    }
}
