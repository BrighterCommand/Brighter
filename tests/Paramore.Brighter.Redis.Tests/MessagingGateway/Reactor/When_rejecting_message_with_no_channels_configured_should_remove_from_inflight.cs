#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Linq;
using Paramore.Brighter.MessagingGateway.Redis;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway.Reactor;

[Collection("Redis Shared Pool")]
[Trait("Category", "Redis")]
[Trait("Fragile", "CI")]
public class RedisMessageConsumerNoChannelsRejectTests : IDisposable
{
    private readonly RedisMessageProducer _messageProducer;
    private readonly RedisMessageConsumer _consumer;
    private readonly Message _message;

    public RedisMessageConsumerNoChannelsRejectTests()
    {
        var configuration = RedisFixture.RedisMessagingGatewayConfiguration();

        var topic = new RoutingKey($"no-channels-test-{Guid.NewGuid()}");
        var queueName = new ChannelName($"no-channels-test-{Guid.NewGuid()}");

        _messageProducer = new RedisMessageProducer(configuration,
            new RedisMessagePublication { Topic = topic });

        // No deadLetterRoutingKey, no invalidMessageRoutingKey
        _consumer = new RedisMessageConsumer(configuration, queueName, topic);

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("test content"));
    }

    [Fact]
    public void When_rejecting_message_with_no_channels_configured_should_remove_from_inflight()
    {
        //Arrange - subscribe then send
        _consumer.Receive(TimeSpan.FromMilliseconds(1000));
        _messageProducer.Send(_message);
        var receivedMessage = _consumer.Receive(TimeSpan.FromMilliseconds(1000)).Single();

        //Act - reject with DeliveryError, but no channels configured
        var result = _consumer.Reject(receivedMessage,
            new MessageRejectionReason(RejectionReason.DeliveryError, "Test delivery error"));

        //Assert - reject returns true and consumer can receive again without "unacked message" error
        Assert.True(result);

        // This would throw ChannelFailureException("Unacked message still in flight...")
        // if reject didn't remove from inflight
        var nextMessages = _consumer.Receive(TimeSpan.FromMilliseconds(1000));
        Assert.Empty(nextMessages);
    }

    public void Dispose()
    {
        _consumer.Purge();
        _consumer.Dispose();
        _messageProducer.Dispose();
    }
}
