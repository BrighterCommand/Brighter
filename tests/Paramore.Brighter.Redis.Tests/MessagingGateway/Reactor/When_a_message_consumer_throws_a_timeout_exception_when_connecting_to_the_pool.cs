using System;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.Redis;
using Paramore.Brighter.Redis.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway.Reactor;

[Collection("Redis Shared Pool")]   //shared connection pool so run sequentially
[Trait("Category", "Redis")]
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

    [Fact]
    public void When_a_message_consumer_throws_a_timeout_exception_when_getting_a_client_from_the_pool()
    {
        _exception = Catch.Exception(() => _messageConsumer.Receive(TimeSpan.FromMilliseconds(1000))); 
            
        _exception.Should().BeOfType<ChannelFailureException>();
        _exception?.InnerException.Should().BeOfType<TimeoutException>();
  
    }
    
    //don't try to dispose, not a real client
}
