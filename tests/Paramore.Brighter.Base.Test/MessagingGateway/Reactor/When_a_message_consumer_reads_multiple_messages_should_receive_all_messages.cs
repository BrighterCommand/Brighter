using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Paramore.Brighter.Extensions;
using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Reactor;

public abstract partial class MessagingGatewayReactorTests<TPublication, TSubscription> : IAsyncLifetime
{
    [Fact]
    public void When_a_message_consumer_reads_multiple_messages_should_receive_all_messages()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = CreateProducer(Publication);
        Channel = CreateChannel(Subscription);

        List<Message> messages =
        [
            CreateMessage(Publication.Topic!),
            CreateMessage(Publication.Topic!),
            CreateMessage(Publication.Topic!),
            CreateMessage(Publication.Topic!)
        ];

        messages.Each(message => Producer.Send(message));
        
        Thread.Sleep(DelayForReceiveMessage);
        
        // Act
        var total = messages.Count;
        for (var i = 0; i < total; i++)
        {
            var received = ReceiveMessage();
            
            // Assert
            Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
            
            var expectedMessage = messages.FirstOrDefault(x => x.Header.MessageId == received.Header.MessageId);
            Assert.NotNull(expectedMessage);
            
            AssertMessageAreEquals(expectedMessage, received);
            Channel.Acknowledge(received);

            if ((i + 1) % Subscription.BufferSize == 0)
            {
                Thread.Sleep(DelayForReceiveMessage);
            }
        }
    }
}
