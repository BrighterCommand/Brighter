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
using Paramore.Brighter.MessagingGateway.Postgres;
using Paramore.Brighter.PostgresSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway;

[Trait("Category", "PostgresSql")]
public class PostgresMessageConsumerNoChannelsConfiguredTests : IDisposable
{
    private readonly IAmAMessageProducerSync _producer;
    private readonly IAmAMessageConsumerSync _consumer;
    private readonly RoutingKey _topic;

    public PostgresMessageConsumerNoChannelsConfiguredTests()
    {
        var testHelper = new PostgresSqlTestHelper();
        testHelper.SetupDatabase();

        _topic = new RoutingKey($"nochan-test-{Guid.NewGuid()}");

        // No deadLetterRoutingKey, no invalidMessageRoutingKey
        var sub = new PostgresSubscription<MyCommand>(
            new SubscriptionName($"nochan-test-{Guid.NewGuid()}"),
            new ChannelName(_topic),
            _topic,
            messagePumpType: MessagePumpType.Reactor);

        var connection = new PostgresMessagingGatewayConnection(testHelper.Configuration);

        var producerRegistry = new PostgresProducerRegistryFactory(
            connection,
            [new PostgresPublication { Topic = _topic }]
        ).Create();

        _producer = (IAmAMessageProducerSync)producerRegistry.LookupBy(_topic);

        var consumerFactory = new PostgresConsumerFactory(connection);
        _consumer = consumerFactory.Create(sub);
    }

    [Fact]
    public void When_rejecting_message_with_no_channels_configured_should_delete_and_log_warning()
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

        // Assert - reject returns true (source deleted, message silently dropped)
        Assert.True(result);

        // Assert - source message is deleted (re-reading returns MT_NONE)
        var sourceMessage = ConsumeMessage(_consumer);
        Assert.Equal(MessageType.MT_NONE, sourceMessage.Header.MessageType);

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
        ((IDisposable)_producer).Dispose();
    }
}
