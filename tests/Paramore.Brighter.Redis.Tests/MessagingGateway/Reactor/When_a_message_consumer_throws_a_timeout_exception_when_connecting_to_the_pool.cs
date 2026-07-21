using System;
using Paramore.Brighter.MessagingGateway.Redis;
using Paramore.Brighter.Redis.Tests.TestDoubles;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway.Reactor;

[Category("Redis")]
public class RedisMessageConsumerOperationInterruptedTests
{
    private readonly ChannelName _queueName = new("test");
    private readonly RoutingKey _topic = new("test");
    private readonly RedisMessageConsumer _messageConsumer;
    private Exception? _exception;

    public RedisMessageConsumerOperationInterruptedTests()
    {
        var configuration = RedisFixture.RedisMessagingGatewayConfiguration();

        _messageConsumer = new RedisMessageConsumerTimeoutOnGetClient(configuration, _queueName, _topic);
    }

    [Test]
    public async Task When_a_message_consumer_throws_a_timeout_exception_when_getting_a_client_from_the_pool()
    {
        _exception = Catch.Exception(() => _messageConsumer.Receive(TimeSpan.FromMilliseconds(1000)));

        await Assert.That(_exception).IsTypeOf<ChannelFailureException>();
        await Assert.That(_exception?.InnerException).IsTypeOf<TimeoutException>();
    }

    //don't try to dispose, not a real client
}

