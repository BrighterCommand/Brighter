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
using System.Net.Mime;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.TestDoubles;
using Paramore.Brighter.RocketMQ.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Reactor;

[Trait("Category", "RocketMQ")]
public class RocketMqNoChannelsConfiguredTests : IDisposable
{
    private readonly RocketMqMessageProducer _producer;
    private readonly IAmAMessageConsumerSync _consumer;
    private readonly Message _message;

    public RocketMqNoChannelsConfiguredTests()
    {
        var sourceTopic = new RoutingKey("rmq_dlq_source");

        var connection = GatewayFactory.CreateConnection();

        // Source topic producer
        var publication = new RocketMqPublication { Topic = sourceTopic };
        _producer = new RocketMqMessageProducer(
            connection,
            GatewayFactory.CreateProducer(connection, publication).GetAwaiter().GetResult(),
            publication);

        // Source topic consumer with NO DLQ or invalid routing keys
        var sourceSub = new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName($"nochannels-test-{Guid.NewGuid()}"),
            channelName: new ChannelName(sourceTopic),
            routingKey: sourceTopic,
            consumerGroup: Guid.NewGuid().ToString(),
            messagePumpType: MessagePumpType.Reactor);

        var consumerFactory = new RocketMessageConsumerFactory(connection);
        _consumer = consumerFactory.Create(sourceSub);

        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), sourceTopic, MessageType.MT_COMMAND,
                contentType: new ContentType(MediaTypeNames.Text.Plain)),
            new MessageBody(JsonSerializer.Serialize(
                (object)new MyCommand { Value = "Test No Channels" }, JsonSerialisationOptions.Options)));
    }

    [Fact]
    public void When_rejecting_message_with_no_channels_configured_should_ack_and_return_true()
    {
        // Arrange - send a message and consume it from the source topic
        _consumer.Purge();
        _producer.Send(_message);
        var receivedMessage = ConsumeMessage(_consumer);

        // Act - reject with DeliveryError (no channels configured)
        var result = _consumer.Reject(receivedMessage,
            new MessageRejectionReason(RejectionReason.DeliveryError, "Test delivery error"));

        // Assert - reject returns true (source Ack'd, breaking requeue loop)
        Assert.True(result);

        // Assert - consumer can continue to receive subsequent messages (not stuck in requeue loop)
        var followUpMessage = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), receivedMessage.Header.Topic, MessageType.MT_COMMAND,
                contentType: new ContentType(MediaTypeNames.Text.Plain)),
            new MessageBody(JsonSerializer.Serialize(
                (object)new MyCommand { Value = "Follow-up" }, JsonSerialisationOptions.Options)));

        _producer.Send(followUpMessage);
        var nextMessage = ConsumeMessage(_consumer);
        Assert.NotEqual(MessageType.MT_NONE, nextMessage.Header.MessageType);
        Assert.Equal(followUpMessage.Body.Value, nextMessage.Body.Value);
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
        } while (maxTries <= 5);

        return new Message();
    }

    public void Dispose()
    {
        _consumer.Purge();
        _consumer.Dispose();
        _producer.Dispose();
    }
}
