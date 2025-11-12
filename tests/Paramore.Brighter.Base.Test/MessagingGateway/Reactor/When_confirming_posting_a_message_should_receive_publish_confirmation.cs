using System.Threading;
using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Reactor;

public abstract partial class MessagingGatewayReactorTests<TPublication, TSubscription> : IAsyncLifetime
{
    [Fact]
    public void When_confirming_posting_a_message_should_receive_publish_confirmation()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = CreateProducer(Publication);

        if (Producer is not ISupportPublishConfirmation confirmation)
        {
            return;
        }

        var messageSent = false;
        confirmation.OnMessagePublished += (confirmed, _) => messageSent = confirmed; 

        var message = CreateMessage(Publication.Topic!);
        
        // Act
        Producer.Send(message);
        Thread.Sleep(DelayForReceiveMessage);
        
        // Assert
        Assert.True(messageSent);
    }
}
