using System;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Proactor;

public class RmqWithTtlProactorTests : RmqProactorTests
{
    protected override TimeSpan? Ttl => TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_consuming_expired_message_should_return_none_message()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = await CreateProducerAsync(Publication);
        Channel = await CreateChannelAsync(Subscription);

        var messageOne = CreateMessage(Publication.Topic!);
        await Producer.SendAsync(messageOne);

        await Task.Delay(TimeSpan.FromSeconds(11));

        var receive = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.Equal(MessageType.MT_NONE,  receive.Header.MessageType);
    }
    
    [Fact]
    public async Task When_consuming_expired_message_should_be_move_to_dead_letter_queue()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName(), setupDeadLetterQueue: true);
        Producer = await CreateProducerAsync(Publication);
        Channel = await CreateChannelAsync(Subscription);

        var messageOne = CreateMessage(Publication.Topic!);
        await Producer.SendAsync(messageOne);

        await Task.Delay(TimeSpan.FromSeconds(11));

        var receive = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.Equal(MessageType.MT_NONE,  receive.Header.MessageType);
        
        receive = await GetMessageFromDeadLetterQueueAsync(Subscription);
        AssertMessageAreEquals(messageOne, receive);
    }
}
