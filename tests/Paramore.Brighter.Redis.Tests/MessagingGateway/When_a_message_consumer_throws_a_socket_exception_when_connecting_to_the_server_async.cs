using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.Redis;
using Paramore.Brighter.Redis.Tests.TestDoubles;
using ServiceStack.Redis;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway;

[Collection("Redis Shared Pool")]   //shared connection pool so run sequentially
[Trait("Category", "Redis")]
public class RedisMessageConsumerRedisNotAvailableTestsAsync : IAsyncDisposable, IDisposable
{
    private readonly ChannelName _queueName = new ChannelName("test");
    private readonly RoutingKey _topic = new RoutingKey("test");
    private readonly RedisMessageConsumer _messageConsumer;
    private Exception? _exception;

    public RedisMessageConsumerRedisNotAvailableTestsAsync()
    {
        var configuration = RedisFixture.RedisMessagingGatewayConfiguration();

        _messageConsumer = new RedisMessageConsumerSocketErrorOnGetClient(configuration, _queueName, _topic);
    }

    [Fact]
    public async Task When_a_message_consumer_throws_a_socket_exception_when_connecting_to_the_server_async()
    {
        _exception = await Catch.ExceptionAsync(() => _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000)));

        _exception.Should().BeOfType<ChannelFailureException>();
        _exception?.InnerException.Should().BeOfType<RedisException>();
    }

    public async ValueTask DisposeAsync()
    {
        await _messageConsumer.PurgeAsync();
        await _messageConsumer.DisposeAsync();
    }

    public void Dispose()
    {
        _messageConsumer.Purge();
        _messageConsumer.Dispose();
    }
}
