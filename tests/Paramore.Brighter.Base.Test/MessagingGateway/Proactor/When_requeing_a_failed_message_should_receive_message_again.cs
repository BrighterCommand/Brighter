using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Proactor;

public abstract partial class MessagingGatewayProactorTests<TPublication, TSubscription> 
{
    [Fact]
    public virtual async Task When_requeing_a_failed_message_should_receive_message_again()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = await CreateProducerAsync(Publication);
        Channel = await CreateChannelAsync(Subscription);
        
        var message = CreateMessage(Publication.Topic!);
        await Producer.SendAsync(message);
        
        await Task.Delay(DelayForReceiveMessage);
        
        // Act
        var received = await ReceiveMessageAsync();
        Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);

        await Channel.RequeueAsync(received);

        await Task.Delay(DelayForRequeueMessage);
        received = await ReceiveMessageAsync(true);
        
        // Assert
        AssertMessageAreEquals(message, received); 
    }
}
