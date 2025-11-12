using System;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Proactor;

public abstract partial class MessagingGatewayProactorTests<TPublication, TSubscription> 
{
    [Fact]
    public async Task When_requeing_a_failed_message_with_delay_should_receive_message_again()
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
        await Producer.SendWithDelayAsync(message, TimeSpan.FromSeconds(5));
        await Task.Delay(TimeSpan.FromSeconds(6));
        
        // Act
        var received = await ReceiveMessageAsync();
        Assert.NotEqual(MessageType.MT_QUIT,  received.Header.MessageType);
        
        await Channel.RequeueAsync(received);
        
        // Assert
        received = await ReceiveMessageAsync();
        await Channel.AcknowledgeAsync(received);
        AssertMessageAreEquals(message, received);
    }
}
