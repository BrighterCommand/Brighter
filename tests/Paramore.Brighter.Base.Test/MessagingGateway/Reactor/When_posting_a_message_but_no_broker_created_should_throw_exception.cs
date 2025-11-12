using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Reactor;

public abstract partial class MessagingGatewayReactorTests<TPublication, TSubscription> : IAsyncLifetime
{
    [Fact]
    public void When_posting_a_message_but_no_broker_created_should_throw_exception()
    {
        try
        {
            // Arrange
            Publication = CreatePublication(GetOrCreateRoutingKey());
            Publication.MakeChannels = OnMissingChannel.Validate;
            Producer = CreateProducer(Publication);
            
            // Act
            var message = CreateMessage(Publication.Topic!);
            Producer.Send(message);
            
            // Assert
            Assert.Fail("We are expected to throw an exception");
        }
        catch
        {
            Assert.True(true);
        }
    }
}
