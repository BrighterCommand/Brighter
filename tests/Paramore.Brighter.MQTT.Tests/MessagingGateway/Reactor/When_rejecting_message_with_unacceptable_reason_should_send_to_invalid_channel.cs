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

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway.Reactor;

[Category("MQTT")]
public class MqttMessageConsumerRejectUnacceptableInvalidChannelTests : IDisposable
{
    private const string SOURCE_TOPIC_PREFIX = "BrighterTests/InvalidSource";
    private const string DLQ_TOPIC_PREFIX = "BrighterTests/InvalidDlq";
    private const string INVALID_TOPIC_PREFIX = "BrighterTests/InvalidTarget";

    private MqttTestServer? _mqttTestServer;
    private readonly MqttMessageProducer _sourceProducer;
    private readonly MqttMessageConsumer _sourceConsumer;
    private readonly MqttMessageConsumer _invalidConsumer;
    private readonly MqttMessageConsumer _dlqConsumer;
    private readonly MqttFactory _mqttFactory;
    private readonly int _serverPort;

    public MqttMessageConsumerRejectUnacceptableInvalidChannelTests()
    {
        var mqttFactory = new MqttFactory();
        int serverPort = MqttTestServer.GetRandomServerPort();

        _mqttFactory = mqttFactory;
        _serverPort = serverPort;

        // Server must be running before producer/consumer ctors connect.
        _mqttTestServer = MqttTestServer.CreateTestMqttServer(mqttFactory, true, serverPort: serverPort);

        //Arrange — source producer
        var producerConfig = new MqttMessagingGatewayProducerConfiguration
        {
            Hostname = IPAddress.Loopback.ToString(),
            Port = serverPort,
            TopicPrefix = SOURCE_TOPIC_PREFIX,
            ClientID = "BrighterTests-Invalid-Producer"
        };
        var publisher = new MqttMessagePublisher(producerConfig);
        _sourceProducer = new MqttMessageProducer(publisher, new Publication());

        //Arrange — source consumer with both DLQ and invalid message routing keys
        var consumerConfig = new MqttMessagingGatewayConsumerConfiguration
        {
            Hostname = IPAddress.Loopback.ToString(),
            Port = serverPort,
            TopicPrefix = SOURCE_TOPIC_PREFIX,
            ClientID = "BrighterTests-Invalid-Consumer"
        };
        _sourceConsumer = new MqttMessageConsumer(
            consumerConfig,
            deadLetterRoutingKey: new RoutingKey(DLQ_TOPIC_PREFIX),
            invalidMessageRoutingKey: new RoutingKey(INVALID_TOPIC_PREFIX)
        );

        //Arrange — invalid message consumer
        var invalidConsumerConfig = new MqttMessagingGatewayConsumerConfiguration
        {
            Hostname = IPAddress.Loopback.ToString(),
            Port = serverPort,
            TopicPrefix = INVALID_TOPIC_PREFIX,
            ClientID = "BrighterTests-InvalidTarget-Consumer"
        };
        _invalidConsumer = new MqttMessageConsumer(invalidConsumerConfig);

        //Arrange — DLQ consumer (should NOT receive the message)
        var dlqConsumerConfig = new MqttMessagingGatewayConsumerConfiguration
        {
            Hostname = IPAddress.Loopback.ToString(),
            Port = serverPort,
            TopicPrefix = DLQ_TOPIC_PREFIX,
            ClientID = "BrighterTests-InvalidDlq-Consumer"
        };
        _dlqConsumer = new MqttMessageConsumer(dlqConsumerConfig);
    }

    [Test]
    public async Task When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel()
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
        await Assert.That(received).IsNotEmpty();
        var sourceMessage = received.First(m => m.Header.MessageType != MessageType.MT_NONE);

        //Act — reject with Unacceptable
        var result = ((IAmAMessageConsumerSync)_sourceConsumer).Reject(
            sourceMessage,
            new MessageRejectionReason(RejectionReason.Unacceptable, "deserialization failed")
        );

        //Assert — reject returns true
        await Assert.That(result).IsTrue();

        //Assert — invalid message consumer receives the rejected message
        await Task.Delay(500);
        var invalidMessages = ((IAmAMessageConsumerSync)_invalidConsumer).Receive(TimeSpan.FromSeconds(2));
        await Assert.That(invalidMessages).IsNotEmpty();
        var invalidMessage = invalidMessages.First(m => m.Header.MessageType != MessageType.MT_NONE);

        await Assert.That(invalidMessage.Body.Value).IsEqualTo(message.Body.Value);
        await Assert.That(invalidMessage.Header.Bag["rejectionReason"]!.ToString()).IsEqualTo("Unacceptable");

        //Assert — DLQ consumer does NOT receive the message
        var dlqMessages = ((IAmAMessageConsumerSync)_dlqConsumer).Receive(TimeSpan.FromMilliseconds(500));
        await Assert.That(dlqMessages).All((Message m) => m.Header.MessageType == MessageType.MT_NONE);
    }

    public void Dispose()
    {
        _sourceProducer.Dispose();
        _sourceConsumer.Dispose();
        _invalidConsumer.Dispose();
        _dlqConsumer.Dispose();
        _mqttTestServer?.Dispose();
    }
}

