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

public class KafkaProactorTests : MessagingGatewayProactorTests<KafkaPublication, KafkaSubscription>
{
    protected override bool HasSupportToPartitionKey => true;
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
        return new KafkaPublication
        {
            Topic = routingKey,
            NumPartitions = 1,
            ReplicationFactor = 1,
            MakeChannels = OnMissingChannel.Create
        };
    }

    protected override KafkaSubscription CreateSubscription(RoutingKey routingKey, ChannelName channelName,
        OnMissingChannel makeChannel = OnMissingChannel.Create, bool setupDeadLetterQueue = false)
    {
        return new KafkaSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.NewAsString()),
            routingKey: routingKey,
            channelName: channelName,
            makeChannels: makeChannel,
            numOfPartitions: 1,
            replicationFactor: 1,
            groupId: Uuid.NewAsString());
    }

    protected override async Task<IAmAMessageProducerAsync> CreateProducerAsync(KafkaPublication publication, CancellationToken cancellationToken = default)
    {
        var produces = await new KafkaMessageProducerFactory(
                new KafkaMessagingGatewayConfiguration { BootStrapServers = ["localhost:9092"] },
                [publication])
            .CreateAsync();

        var producer = produces.First().Value;
        
        return (IAmAMessageProducerAsync)producer;
    }

    protected override async Task<IAmAChannelAsync> CreateChannelAsync(KafkaSubscription subscription, CancellationToken cancellationToken = default)
    {
        var channel = await new ChannelFactory(new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration { BootStrapServers = ["localhost:9092"] }))
            .CreateAsyncChannelAsync(subscription, cancellationToken);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            // Ensuring that the queue exists before return the channel
            await channel.ReceiveAsync(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
        
        return channel;
    }
    
    [Fact]
    public async Task When_a_message_is_sent_keep_order()
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
            
            AssertMessageAreEquals(messages[i], received);
            await Channel.AcknowledgeAsync(received);

            if ((i + 1) % Subscription.BufferSize == 0)
            {
                await Task.Delay(DelayForReceiveMessage);
            }
        }
    }

    [Fact]
    public override Task When_requeing_a_failed_message_should_receive_message_again()
    {
        // Kafka doesn't support reuqueing
        return Task.CompletedTask;
    }


    protected override Task<Message> ReceiveMessageAsync(bool retryOnNoneMessage = false)
    {
        return base.ReceiveMessageAsync(true);
    }
}
