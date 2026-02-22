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

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway.Proactor;

[Trait("Category", "MQTT")]
[Collection("MQTT")]
public class MqttMessageConsumerRejectDeliveryErrorDlqAsyncTests : IDisposable
{
    private const string SOURCE_TOPIC_PREFIX = "BrighterTests/DlqAsyncSource";
    private const string DLQ_TOPIC_PREFIX = "BrighterTests/DlqAsyncTarget";

    private readonly MqttTestServer? _mqttTestServer;
    private readonly MqttMessageProducer _sourceProducer;
    private readonly MqttMessageConsumer _sourceConsumer;
    private readonly MqttMessageConsumer _dlqConsumer;

    public MqttMessageConsumerRejectDeliveryErrorDlqAsyncTests(ITestOutputHelper outputHelper)
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
            ClientID = "BrighterTests-DlqAsync-Producer"
        };
        var publisher = new MqttMessagePublisher(producerConfig);
        _sourceProducer = new MqttMessageProducer(publisher, new Publication());

        //Arrange — source consumer with DLQ routing key
        var consumerConfig = new MqttMessagingGatewayConsumerConfiguration
        {
            Hostname = IPAddress.Loopback.ToString(),
            Port = serverPort,
            TopicPrefix = SOURCE_TOPIC_PREFIX,
            ClientID = "BrighterTests-DlqAsync-Consumer"
        };
        _sourceConsumer = new MqttMessageConsumer(
            consumerConfig,
            deadLetterRoutingKey: new RoutingKey(DLQ_TOPIC_PREFIX)
        );

        //Arrange — DLQ consumer
        var dlqConsumerConfig = new MqttMessagingGatewayConsumerConfiguration
        {
            Hostname = IPAddress.Loopback.ToString(),
            Port = serverPort,
            TopicPrefix = DLQ_TOPIC_PREFIX,
            ClientID = "BrighterTests-DlqAsyncTarget-Consumer"
        };
        _dlqConsumer = new MqttMessageConsumer(dlqConsumerConfig);
    }

    [Fact]
    public async Task When_rejecting_message_async_with_delivery_error_should_send_to_dlq()
    {
        //Arrange
        var routingKey = new RoutingKey("test.orders");
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("test content")
        );

        //Act — send and wait for broker delivery
        await _sourceProducer.SendAsync(message);
        await Task.Delay(500);

        var received = await _sourceConsumer.ReceiveAsync(TimeSpan.FromSeconds(2));
        Assert.NotEmpty(received);
        var sourceMessage = received.First(m => m.Header.MessageType != MessageType.MT_NONE);

        //Act — reject async with DeliveryError
        var result = await _sourceConsumer.RejectAsync(
            sourceMessage,
            new MessageRejectionReason(RejectionReason.DeliveryError, "handler threw exception")
        );

        //Assert — reject returns true
        Assert.True(result);

        //Assert — DLQ consumer receives the rejected message
        await Task.Delay(500);
        var dlqMessages = await _dlqConsumer.ReceiveAsync(TimeSpan.FromSeconds(2));
        Assert.NotEmpty(dlqMessages);
        var dlqMessage = dlqMessages.First(m => m.Header.MessageType != MessageType.MT_NONE);

        //Assert — message body preserved
        Assert.Equal(message.Body.Value, dlqMessage.Body.Value);

        //Assert — rejection metadata present
        Assert.Equal(routingKey.Value, dlqMessage.Header.Bag["originalTopic"]!.ToString());
        Assert.Equal("DeliveryError", dlqMessage.Header.Bag["rejectionReason"]!.ToString());
        Assert.True(dlqMessage.Header.Bag.ContainsKey("rejectionTimestamp"));
        Assert.Equal("MT_COMMAND", dlqMessage.Header.Bag["originalMessageType"]!.ToString());
    }

    public void Dispose()
    {
        _sourceProducer.Dispose();
        _sourceConsumer.Dispose();
        _dlqConsumer.Dispose();
        _mqttTestServer?.Dispose();
    }
}
