using System;
using System.Threading.Tasks;
using Xunit;
using Paramore.Brighter.MessagingGateway.Redis;
using Paramore.Brighter.Redis.Tests.TestDoubles;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway.Proactor;

[Collection("Redis Shared Pool")]   //shared connection pool so run sequentially
[Trait("Category", "Redis")]
public class RedisMessageConsumerOperationInterruptedTestsAsync
{
    private readonly ChannelName _queueName = new("test");
    private readonly RoutingKey _topic = new("test");
    private readonly RedisMessageConsumer _messageConsumer;
    private Exception? _exception;

    public RedisMessageConsumerOperationInterruptedTestsAsync()
    {
        var configuration = RedisFixture.RedisMessagingGatewayConfiguration();

        _messageConsumer = new RedisMessageConsumerTimeoutOnGetClient(configuration, _queueName, _topic);
    }

    [Fact]
    public async Task When_a_message_consumer_throws_a_timeout_exception_when_getting_a_client_from_the_pool_async()
    {
        _exception = await Catch.ExceptionAsync(() => _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000)));

        Assert.IsType<ChannelFailureException>(_exception);
        Assert.IsType<TimeoutException>(_exception?.InnerException);
    }

    //do not dispose a fake client
}
