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
using System.Net;
using System.Threading.Tasks;
using MQTTnet;
using Paramore.Brighter.MessagingGateway.MQTT;
using Paramore.Brighter.MQTT.Tests.MessagingGateway.Helpers.Server;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway.Reactor;

[Trait("Category", "MQTT")]
[Collection("MQTT")]
public class MqttMessageConsumerRejectUnacceptableFallbackToDlqTests : IDisposable
{
    private const string SOURCE_TOPIC_PREFIX = "BrighterTests/FallbackSource";
    private const string DLQ_TOPIC_PREFIX = "BrighterTests/FallbackDlq";

    private readonly MqttTestServer? _mqttTestServer;
    private readonly MqttMessageProducer _sourceProducer;
    private readonly MqttMessageConsumer _sourceConsumer;
    private readonly MqttMessageConsumer _dlqConsumer;

    public MqttMessageConsumerRejectUnacceptableFallbackToDlqTests(ITestOutputHelper outputHelper)
    {
        var mqttFactory = new MqttFactory();
        int serverPort = MqttTestServer.GetRandomServerPort();

        _mqttTestServer = MqttTestServer.CreateTestMqttServer(mqttFactory, true, serverPort: serverPort);

        //Arrange — source producer
        var producerConfig = new MqttMessagingGatewayProducerConfiguration
        {
            Hostname = IPAddress.Loopback.ToString(),
            Port = serverPort,
            TopicPrefix = SOURCE_TOPIC_PREFIX,
            ClientID = "BrighterTests-Fallback-Producer"
        };
        var publisher = new MqttMessagePublisher(producerConfig);
        _sourceProducer = new MqttMessageProducer(publisher, new Publication());

        //Arrange — source consumer with DLQ only (no invalid message routing key)
        var consumerConfig = new MqttMessagingGatewayConsumerConfiguration
        {
            Hostname = IPAddress.Loopback.ToString(),
            Port = serverPort,
            TopicPrefix = SOURCE_TOPIC_PREFIX,
            ClientID = "BrighterTests-Fallback-Consumer"
        };
        _sourceConsumer = new MqttMessageConsumer(
            consumerConfig,
            deadLetterRoutingKey: new RoutingKey(DLQ_TOPIC_PREFIX)
        );

        //Arrange — DLQ consumer (should receive the fallback)
        var dlqConsumerConfig = new MqttMessagingGatewayConsumerConfiguration
        {
            Hostname = IPAddress.Loopback.ToString(),
            Port = serverPort,
            TopicPrefix = DLQ_TOPIC_PREFIX,
            ClientID = "BrighterTests-FallbackDlq-Consumer"
        };
        _dlqConsumer = new MqttMessageConsumer(dlqConsumerConfig);
    }

    [Fact]
    public async Task When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq()
    {
        //Arrange
        var routingKey = new RoutingKey("test.orders");
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("test content")
        );

        //Act — send and wait for broker delivery
        ((IAmAMessageProducerSync)_sourceProducer).Send(message);
        await Task.Delay(500);

        var received = ((IAmAMessageConsumerSync)_sourceConsumer).Receive(TimeSpan.FromSeconds(2));
        Assert.NotEmpty(received);
        var sourceMessage = received.First(m => m.Header.MessageType != MessageType.MT_NONE);

        //Act — reject with Unacceptable (no invalid channel configured, should fallback to DLQ)
        var result = ((IAmAMessageConsumerSync)_sourceConsumer).Reject(
            sourceMessage,
            new MessageRejectionReason(RejectionReason.Unacceptable, "deserialization failed")
        );

        //Assert — reject returns true
        Assert.True(result);

        //Assert — DLQ consumer receives the message (fallback)
        await Task.Delay(500);
        var dlqMessages = ((IAmAMessageConsumerSync)_dlqConsumer).Receive(TimeSpan.FromSeconds(2));
        Assert.NotEmpty(dlqMessages);
        var dlqMessage = dlqMessages.First(m => m.Header.MessageType != MessageType.MT_NONE);

        Assert.Equal(message.Body.Value, dlqMessage.Body.Value);
        Assert.Equal("Unacceptable", dlqMessage.Header.Bag["rejectionReason"]!.ToString());
    }

    public void Dispose()
    {
        _sourceProducer.Dispose();
        _sourceConsumer.Dispose();
        _dlqConsumer.Dispose();
        _mqttTestServer?.Dispose();
    }
}
