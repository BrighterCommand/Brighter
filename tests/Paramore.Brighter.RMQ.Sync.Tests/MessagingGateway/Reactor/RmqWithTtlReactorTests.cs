using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Reactor;

public class RmqWithTtlReactorTests : RmqReactorTests
{
    protected override TimeSpan? Ttl => TimeSpan.FromSeconds(10);

    [Fact]
    public void When_consuming_expired_message_should_return_none_message()
    {
        // Arrange
        BufferSize = 1;
        
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = CreateProducer(Publication);
        Channel = CreateChannel(Subscription);

        var messageOne = CreateMessage(Publication.Topic!);
        var messageTwo = CreateMessage(Publication.Topic!);
        Producer.Send(messageOne);
        Producer.Send(messageTwo);

        Thread.Sleep(TimeSpan.FromSeconds(15));

        _ = Channel.Receive(ReceiveTimeout);
        var receive = Channel.Receive(ReceiveTimeout);
        Assert.Equal(MessageType.MT_NONE,  receive.Header.MessageType);
    }
    
    [Fact]
    public void When_consuming_expired_message_should_be_move_to_dead_letter_queue()
    {
        // Arrange
        BufferSize = 1;
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName(), setupDeadLetterQueue: true);
        Producer = CreateProducer(Publication);
        Channel = CreateChannel(Subscription);

        var messageOne = CreateMessage(Publication.Topic!);
        var messageTwo = CreateMessage(Publication.Topic!);
        Producer.Send(messageTwo);
        Producer.Send(messageOne);

        Thread.Sleep(TimeSpan.FromSeconds(15));

        Channel.Receive(ReceiveTimeout);
        var receive = Channel.Receive(ReceiveTimeout);
        Assert.Equal(MessageType.MT_NONE,  receive.Header.MessageType);
        
        receive = GetMessageFromDeadLetterQueue(Subscription);
        AssertMessageAreEquals(messageOne, receive);
    }   
}
