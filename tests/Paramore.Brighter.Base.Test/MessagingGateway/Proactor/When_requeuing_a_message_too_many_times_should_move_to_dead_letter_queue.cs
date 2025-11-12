using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Proactor;

public abstract partial class MessagingGatewayProactorTests<TPublication, TSubscription> 
{
    [Fact]
    public async Task When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue()
    {
        if (!HasSupportToMoveToDeadLetterQueueAfterTooManyRetries)
        {
            return;
        }
        
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName(), setupDeadLetterQueue: true);
        Producer = await CreateProducerAsync(Publication);
        Channel = await CreateChannelAsync(Subscription);
        
        var message = CreateMessage(Publication.Topic!);
        await Producer.SendAsync(message);

        Message? received;
        for (var i = 0; i < Subscription.RequeueCount; i++)
        {
            received = await ReceiveMessageAsync();
            await Channel.RequeueAsync(received);
        }
        
        received = await ReceiveMessageAsync();
        Assert.Equal(MessageType.MT_NONE, received.Header.MessageType);

        received = await GetMessageFromDeadLetterQueueAsync(Subscription);
        
        // Assert
        AssertMessageAreEquals(message, received); 
    }
}
