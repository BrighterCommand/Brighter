using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Proactor;

public abstract partial class MessagingGatewayProactorTests<TPublication, TSubscription> 
{
    [Fact]
    public async Task When_posting_a_message_via_the_messaging_gateway_should_be_received()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = await CreateProducerAsync(Publication);
        Channel = await CreateChannelAsync(Subscription);
        
        var message = CreateMessage(Publication.Topic!);
        
        // Act
        await Producer.SendAsync(message);
        await Task.Delay(DelayForReceiveMessage);
        var received = await ReceiveMessageAsync();
        
        // Assert
        AssertMessageAreEquals(message, received);
    }
}
