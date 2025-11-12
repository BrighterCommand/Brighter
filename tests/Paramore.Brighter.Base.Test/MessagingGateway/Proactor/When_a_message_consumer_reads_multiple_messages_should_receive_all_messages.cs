using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;
using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Proactor;

public abstract partial class MessagingGatewayProactorTests<TPublication, TSubscription> 
{
    [Fact]
    public async Task When_a_message_consumer_reads_multiple_messages_should_receive_all_messages()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = await CreateProducerAsync(Publication);
        Channel = await CreateChannelAsync(Subscription);

        List<Message> messages =
        [
            CreateMessage(Publication.Topic!),
            CreateMessage(Publication.Topic!),
            CreateMessage(Publication.Topic!),
            CreateMessage(Publication.Topic!)
        ];

        await messages.EachAsync(async message => await Producer.SendAsync(message));
        
        await Task.Delay(DelayForReceiveMessage);
        
        // Act
        var total = messages.Count;
        for (var i = 0; i < total; i++)
        {
            var received = await ReceiveMessageAsync();
            
            // Assert
            Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
            
            var expectedMessage = messages.FirstOrDefault(x => x.Header.MessageId == received.Header.MessageId);
            Assert.NotNull(expectedMessage);
            
            AssertMessageAreEquals(expectedMessage, received);
            await Channel.AcknowledgeAsync(received);

            if ((i + 1) % Subscription.BufferSize == 0)
            {
                await Task.Delay(DelayForReceiveMessage);
            }
        }
    }
}
