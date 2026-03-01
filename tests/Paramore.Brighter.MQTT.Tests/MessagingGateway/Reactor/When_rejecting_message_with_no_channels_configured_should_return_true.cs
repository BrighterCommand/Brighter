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
public class MqttMessageConsumerRejectNoChannelsTests : IDisposable
{
    private const string SOURCE_TOPIC_PREFIX = "BrighterTests/NoChannels";

    private readonly MqttTestServer? _mqttTestServer;
    private readonly MqttMessageProducer _sourceProducer;
    private readonly MqttMessageConsumer _sourceConsumer;

    public MqttMessageConsumerRejectNoChannelsTests(ITestOutputHelper outputHelper)
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
            ClientID = "BrighterTests-NoChannels-Producer"
        };
        var publisher = new MqttMessagePublisher(producerConfig);
        _sourceProducer = new MqttMessageProducer(publisher, new Publication());

        //Arrange — source consumer with NO DLQ or invalid message routing keys
        var consumerConfig = new MqttMessagingGatewayConsumerConfiguration
        {
            Hostname = IPAddress.Loopback.ToString(),
            Port = serverPort,
            TopicPrefix = SOURCE_TOPIC_PREFIX,
            ClientID = "BrighterTests-NoChannels-Consumer"
        };
        _sourceConsumer = new MqttMessageConsumer(consumerConfig);
    }

    [Fact]
    public async Task When_rejecting_message_with_no_channels_configured_should_return_true()
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

        //Act — reject with DeliveryError (no channels configured)
        var result = ((IAmAMessageConsumerSync)_sourceConsumer).Reject(
            sourceMessage,
            new MessageRejectionReason(RejectionReason.DeliveryError, "handler threw exception")
        );

        //Assert — reject returns true (not false as before the DLQ work)
        Assert.True(result);
    }

    public void Dispose()
    {
        _sourceProducer.Dispose();
        _sourceConsumer.Dispose();
        _mqttTestServer?.Dispose();
    }
}
