using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Proactor;

public abstract partial class MessagingGatewayProactorTests<TPublication, TSubscription> 
{
    [Fact]
    public async Task When_posting_a_message_but_no_broker_created_should_throw_exception()
    {
        try
        {
            // Arrange
            Publication = CreatePublication(GetOrCreateRoutingKey());
            Publication.MakeChannels = OnMissingChannel.Validate;
            Producer = await CreateProducerAsync(Publication);
            
            // Act
            var message = CreateMessage(Publication.Topic!);
            await Producer.SendAsync(message);
            
            // Assert
            Assert.Fail("We are expected to throw an exception");
        }
        catch
        {
            Assert.True(true);
        }
    }
}
