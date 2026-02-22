#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading;
using Paramore.Brighter.MQTT.Tests.MessagingGateway.Helpers.Base;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway.Reactor;

/// <summary>
/// When the MQTT consumer requeues a message, it should publish a new message to the same topic
/// via a lazily-created producer. Previously Requeue returned false (not implemented); now it
/// delegates to the producer so that requeued messages are actually redelivered.
/// </summary>
[Trait("Category", "MQTT")]
[Collection("MQTT")]
public class MqttConsumerRequeueTests : MqttTestClassBase<MqttConsumerRequeueTests>
{
    private const string ClientId = "BrighterIntegrationTests-Requeue";
    private const string TopicPrefix = "BrighterIntegrationTests/RequeueTests";

    public MqttConsumerRequeueTests(ITestOutputHelper testOutputHelper)
        : base(ClientId, TopicPrefix, testOutputHelper)
    {
    }

    private IAmAMessageProducerSync MessageProducerSync => (MessageProducerAsync as IAmAMessageProducerSync)!;
    private IAmAMessageConsumerSync MessageConsumerSync => (MessageConsumerAsync as IAmAMessageConsumerSync)!;

    [Fact]
    public void When_requeuing_should_publish_message_via_producer()
    {
        // Arrange - send a message and receive it
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("requeue-test"), MessageType.MT_COMMAND),
            new MessageBody("test content for requeue"));

        MessageProducerSync.Send(message);

        var received = ReceiveMessage();

        // Act - requeue the received message
        var result = MessageConsumerSync.Requeue(received);

        // Assert - requeue should return true (was returning false)
        Assert.True(result, "Requeue should succeed by publishing via producer");

        // Note: HandledCount is incremented by the message pump, not by the consumer requeue

        // Assert - message should be available again on the topic (published via producer)
        var requeued = ReceiveMessage();
        Assert.Equal(message.Body.Value, requeued.Body.Value);
    }

    private Message ReceiveMessage()
    {
        int maxTries = 10;
        for (int i = 0; i < maxTries; i++)
        {
            var messages = MessageConsumerSync.Receive(TimeSpan.FromMilliseconds(500));
            if (messages.Length > 0 && messages[0].Header.MessageType != MessageType.MT_NONE)
            {
                return messages[0];
            }

            Thread.Sleep(100);
        }

        throw new Exception($"Failed to receive message after {maxTries} attempts");
    }
}
