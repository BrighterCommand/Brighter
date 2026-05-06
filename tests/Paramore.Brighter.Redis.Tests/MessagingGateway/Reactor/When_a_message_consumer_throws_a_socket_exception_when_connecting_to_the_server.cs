using System;
using Paramore.Brighter.MessagingGateway.Redis;
using Paramore.Brighter.Redis.Tests.TestDoubles;
using ServiceStack.Redis;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway.Reactor;

[Category("Redis")]
public class RedisMessageConsumerRedisNotAvailableTests
{
    private readonly ChannelName _queueName = new ChannelName("test");
    private readonly RoutingKey _topic = new RoutingKey("test");
    private readonly RedisMessageConsumer _messageConsumer;
    private Exception? _exception;

    public RedisMessageConsumerRedisNotAvailableTests()
    {
        var configuration = RedisFixture.RedisMessagingGatewayConfiguration();

        _messageConsumer = new RedisMessageConsumerSocketErrorOnGetClient(configuration, _queueName, _topic);

    }

    [Test]
    public async Task When_a_message_consumer_throws_a_socket_exception_when_connecting_to_the_server()
    {
        _exception = Catch.Exception(() => _messageConsumer.Receive(TimeSpan.FromMilliseconds(1000)));

        await Assert.That(_exception).IsTypeOf<ChannelFailureException>();
        await Assert.That(_exception?.InnerException).IsTypeOf<RedisException>();
    }

    //Do not dispose a fake client
}

