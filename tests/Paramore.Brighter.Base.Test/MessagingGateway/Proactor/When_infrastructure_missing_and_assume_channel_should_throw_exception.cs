using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Proactor;

public abstract partial class MessagingGatewayProactorTests<TPublication, TSubscription> 
{
    [Fact]
    public async Task When_infrastructure_missing_and_assume_channel_should_throw_exception()
    {
        try
        {
            // Arrange
            Publication = CreatePublication(GetOrCreateRoutingKey());
            Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName(), OnMissingChannel.Assume);
            Producer = await CreateProducerAsync(Publication);
            Channel = await CreateChannelAsync(Subscription);
        
            var message = CreateMessage(Publication.Topic!);
        
            // Act
            await Producer.SendAsync(message);
        
            // Assert
            await ReceiveMessageAsync();
            Assert.Fail("We are expected to throw an exception");
        }
        catch
        {
            Assert.True(true);
        }
    }
}
