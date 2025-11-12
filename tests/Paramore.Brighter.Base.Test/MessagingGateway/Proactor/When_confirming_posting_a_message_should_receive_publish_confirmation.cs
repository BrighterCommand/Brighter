using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Proactor;

public abstract partial class MessagingGatewayProactorTests<TPublication, TSubscription> 
{
    [Fact]
    public async Task When_confirming_posting_a_message_should_receive_publish_confirmation()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = await CreateProducerAsync(Publication);

        if (Producer is not ISupportPublishConfirmation confirmation)
        {
            return;
        }

        var messageSent = false;
        confirmation.OnMessagePublished += (confirmed, _) => messageSent = confirmed; 

        var message = CreateMessage(Publication.Topic!);
        
        // Act
        await Producer.SendAsync(message);
        await Task.Delay(DelayForReceiveMessage);
        
        // Assert
        Assert.True(messageSent);
    }
}
