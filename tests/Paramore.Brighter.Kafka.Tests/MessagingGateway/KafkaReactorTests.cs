using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.MessagingGateway;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

public class KafkaReactorTests : MessagingGatewayReactorTests<KafkaPublication, KafkaSubscription>
{
    protected override bool HasSupportToPartitionKey => true;
    protected override TimeSpan DelayForRequeueMessage  => TimeSpan.FromSeconds(15);
    private string Topic { get; } = $"Topic{Uuid.New():N}";
    
    protected override ChannelName GetOrCreateChannelName(string testName = null!)
    {
        return new ChannelName(Topic);
    }

    protected override RoutingKey GetOrCreateRoutingKey(string testName = null!)
    {
        return new RoutingKey(Topic);
    }

    protected override KafkaPublication CreatePublication(RoutingKey routingKey)
    {
        return new KafkaPublication { Topic = routingKey, MakeChannels = OnMissingChannel.Create };
    }

    protected override KafkaSubscription CreateSubscription(RoutingKey routingKey, ChannelName channelName,
        OnMissingChannel makeChannel = OnMissingChannel.Create, bool setupDeadLetterQueue = false)
    {
        return new KafkaSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.NewAsString()),
            routingKey: routingKey,
            channelName: channelName,
            makeChannels: makeChannel,
            groupId:  Uuid.NewAsString());
    }

    protected override IAmAMessageProducerSync CreateProducer(KafkaPublication publication)
    {
        var produces = new KafkaMessageProducerFactory(
                new KafkaMessagingGatewayConfiguration { BootStrapServers = ["localhost:9092"] },
                [publication])
            .Create();

        var producer = produces.First().Value;
        
        return (IAmAMessageProducerSync)producer;
    }

    protected override IAmAChannelSync CreateChannel(KafkaSubscription subscription)
    {
        var channel = new ChannelFactory(new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration { BootStrapServers = ["localhost:9092"] }))
            .CreateSyncChannel(subscription);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            // Ensuring that the queue exists before return the channel
            channel.Receive(TimeSpan.FromMilliseconds(100));
        }
        
        return channel;
    }
    
    [Fact]
    public void When_a_message_is_sent_keep_order()
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
            
            AssertMessageAreEquals(messages[i], received);
            Channel.Acknowledge(received);

            if ((i + 1) % Subscription.BufferSize == 0)
            {
                Task.Delay(DelayForReceiveMessage);
            }
        }
    }

    [Fact]
    public override void When_requeing_a_failed_message_should_receive_message_again()
    {
        // Kafka doesn't support requeuing
    }
}
