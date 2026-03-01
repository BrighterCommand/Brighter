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
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.MSSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

[Trait("Category", "MSSQL")]
public class MsSqlMessageConsumerNoChannelsConfiguredTests : IDisposable
{
    private readonly MsSqlMessageProducer _producer;
    private readonly MsSqlMessageConsumer _consumer;
    private readonly RoutingKey _topic;

    public MsSqlMessageConsumerNoChannelsConfiguredTests()
    {
        var testHelper = new MsSqlTestHelper();
        testHelper.SetupQueueDb();

        _topic = new RoutingKey($"nochan-test-{Guid.NewGuid()}");

        // No deadLetterRoutingKey, no invalidMessageRoutingKey
        var sub = new MsSqlSubscription<MyCommand>(
            new SubscriptionName($"nochan-test-{Guid.NewGuid()}"),
            new ChannelName(_topic),
            _topic,
            messagePumpType: MessagePumpType.Reactor);

        _producer = new MsSqlMessageProducer(testHelper.QueueConfiguration);

        _consumer = (MsSqlMessageConsumer)new MsSqlMessageConsumerFactory(testHelper.QueueConfiguration).Create(sub);
    }

    [Fact]
    public void When_rejecting_message_with_no_channels_configured_should_return_true()
    {
        // Arrange - send a message and consume it
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _topic, MessageType.MT_COMMAND),
            new MessageBody("test content"));
        _producer.Send(message);
        var receivedMessage = ConsumeMessage(_consumer);

        // Act - reject with DeliveryError but no channels configured
        var result = _consumer.Reject(receivedMessage,
            new MessageRejectionReason(RejectionReason.DeliveryError, "Test delivery error"));

        // Assert - reject returns true (message is silently dropped)
        Assert.True(result);

        // Assert - consumer can continue to receive subsequent messages
        var nextMessage = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _topic, MessageType.MT_COMMAND),
            new MessageBody("second message"));
        _producer.Send(nextMessage);
        var received = ConsumeMessage(_consumer);
        Assert.Equal(nextMessage.Id, received.Id);
    }

    private static Message ConsumeMessage(IAmAMessageConsumerSync consumer)
    {
        var maxTries = 0;
        do
        {
            var messages = consumer.Receive(TimeSpan.FromMilliseconds(1000));
            var message = messages.First();
            if (message.Header.MessageType != MessageType.MT_NONE)
                return message;
            maxTries++;
        } while (maxTries <= 3);

        return new Message();
    }

    public void Dispose()
    {
        _consumer.Purge();
        _consumer.Dispose();
        _producer.Dispose();
    }
}
