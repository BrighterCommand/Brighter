using System;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Proactor;

[Collection("MessagingGateway")]
public class RmqWithTtlProactorTests : RmqProactorTests
{
    protected override TimeSpan? Ttl => TimeSpan.FromSeconds(10);

    [Fact]
    public async Task When_consuming_expired_message_should_return_none_message()
    {
        // Arrange
        BufferSize = 1;
        
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = await CreateProducerAsync(Publication);
        Channel = await CreateChannelAsync(Subscription);

        var messageOne = CreateMessage(Publication.Topic!);
        var messageTwo = CreateMessage(Publication.Topic!);
        await Producer.SendAsync(messageOne);
        await Producer.SendAsync(messageTwo);

        await Task.Delay(TimeSpan.FromSeconds(15));

        _ = await Channel.ReceiveAsync(ReceiveTimeout);
        var receive = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.Equal(MessageType.MT_NONE,  receive.Header.MessageType);
    }
    
    [Fact]
    public async Task When_consuming_expired_message_should_be_move_to_dead_letter_queue()
    {
        // Arrange
        BufferSize = 1;
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName(), setupDeadLetterQueue: true);
        Producer = await CreateProducerAsync(Publication);
        Channel = await CreateChannelAsync(Subscription);

        var messageOne = CreateMessage(Publication.Topic!);
        var messageTwo = CreateMessage(Publication.Topic!);
        await Producer.SendAsync(messageTwo);
        await Producer.SendAsync(messageOne);

        await Task.Delay(TimeSpan.FromSeconds(15));

        await Channel.ReceiveAsync(ReceiveTimeout);
        var receive = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.Equal(MessageType.MT_NONE,  receive.Header.MessageType);
        
        receive = await GetMessageFromDeadLetterQueueAsync(Subscription);
        AssertMessageAreEquals(messageOne, receive);
    }
}
