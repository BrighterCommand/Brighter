using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.MessagingGateway;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.MessagingGateway.Redis;
using Paramore.Brighter.Redis.Tests.TestDoubles;
using ServiceStack.Redis;
using Xunit;
using Xunit.Sdk;
using RedisSubscription = Paramore.Brighter.MessagingGateway.Redis.RedisSubscription;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway;

public class RedisProactorTests : MessagingGatewayProactorTests<RedisMessagePublication, RedisSubscription>
{
    private readonly RedisMessagingGatewayConfiguration _configuration = new()
    {
        RedisConnectionString = "redis://localhost:6379?ConnectTimeout=1000&SendTimeout=1000",
        MaxPoolSize = 10,
        MessageTimeToLive = TimeSpan.FromMinutes(10),
        DefaultRetryTimeout = 3000
    };
    
    protected override RedisMessagePublication CreatePublication(RoutingKey routingKey)
    {
        return new RedisMessagePublication
        {
            Topic = routingKey,
            MakeChannels = OnMissingChannel.Create
        };
    }

    protected override RedisSubscription CreateSubscription(RoutingKey routingKey, ChannelName channelName,
        OnMissingChannel makeChannel = OnMissingChannel.Create, bool setupDeadLetterQueue = false)
    {
        return new RedisSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.NewAsString()),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: makeChannel,
            bufferSize: 2);
    }

    protected override async Task<IAmAMessageProducerAsync> CreateProducerAsync(RedisMessagePublication publication, CancellationToken cancellationToken = default)
    {
        var produces = await new RedisMessageProducerFactory(
                  _configuration,
                [publication])
            .CreateAsync();

        var producer = produces.First().Value;
        
        return (IAmAMessageProducerAsync)producer;
    }

    protected override async Task<IAmAChannelAsync> CreateChannelAsync(RedisSubscription subscription, CancellationToken cancellationToken = default)
    {
        var channel = await new ChannelFactory(new RedisMessageConsumerFactory(_configuration))
            .CreateAsyncChannelAsync(subscription, cancellationToken);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            // Ensuring that the queue exists before return the channel
            await channel.ReceiveAsync(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
        
        return channel;
    }
    
    [Fact]
    public async Task When_a_message_consumer_throws_a_socket_exception_when_connecting_to_the_server_async()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());

        try
        {
            var messageConsumer = new RedisMessageConsumerSocketErrorOnGetClient(_configuration, 
                Subscription.ChannelName,
                Publication.Topic!);
            await messageConsumer.ReceiveAsync(ReceiveTimeout);
            Assert.Fail("Expected exception");
        }
        catch (Exception e) when(e is not XunitException)
        {
            Assert.IsType<ChannelFailureException>(e);
            Assert.IsType<RedisException>(e.InnerException);
        }
    }
    
    [Fact]
    public async Task When_a_message_consumer_throws_a_timeout_exception_when_getting_a_client_from_the_pool()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());

        try
        {
            var messageConsumer = new RedisMessageConsumerTimeoutOnGetClient(_configuration, 
                Subscription.ChannelName,
                Publication.Topic!);
            await messageConsumer.ReceiveAsync(ReceiveTimeout);
            Assert.Fail("Expected exception");
        }
        catch (Exception e) when(e is not XunitException)
        {
            Assert.IsType<ChannelFailureException>(e);
            Assert.IsType<TimeoutException>(e.InnerException);
        }
    }
}
