using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Proactor;

public abstract partial class MessagingGatewayProactorTests<TPublication, TSubscription> 
{
    [Fact]
    public async Task When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery()
    {
        if (!HasSupportToDelayedMessages)
        {
            return;
        }
        
        // arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = await CreateProducerAsync(Publication);
        Channel = await CreateChannelAsync(Subscription);

        var message = CreateMessage(Publication.Topic!);
        await Producer.SendWithDelayAsync(message, MessageDelay);
        
        // Act
        var received = await ReceiveMessageAsync();
        Assert.Equal(MessageType.MT_NONE,  received.Header.MessageType);
        
        await Task.Delay(MessageDelay);
        
        // Assert
        received = await ReceiveMessageAsync();
        AssertMessageAreEquals(message, received);
    }
}
