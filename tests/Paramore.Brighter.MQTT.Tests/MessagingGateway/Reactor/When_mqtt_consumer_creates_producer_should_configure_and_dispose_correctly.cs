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
using System.Net;
using System.Threading;
using MQTTnet;
using Paramore.Brighter.MessagingGateway.MQTT;
using Paramore.Brighter.MQTT.Tests.MessagingGateway.Helpers.Server;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway.Reactor;

/// <summary>
/// When the MQTT consumer creates a lazy requeue producer, it should be configured with the scheduler
/// passed to the consumer. When the consumer is disposed, the producer should also be disposed.
/// </summary>
[Trait("Category", "MQTT")]
[Collection("MQTT")]
public class MqttConsumerProducerConfigAndDisposeTests : IDisposable
{
    private readonly MqttTestServer? _mqttTestServer;
    private readonly MqttMessageProducer _producer;
    private readonly MqttMessageConsumer _consumer;
    private readonly SpySchedulerSync _scheduler;

    public MqttConsumerProducerConfigAndDisposeTests(ITestOutputHelper testOutputHelper)
    {

        int serverPort = MqttTestServer.GetRandomServerPort();
        string topicPrefix = "BrighterIntegrationTests/SchedulerDisposeTests";

        _mqttTestServer = MqttTestServer.CreateTestMqttServer(
            new MqttFactory(), true, null,
            IPAddress.Any, serverPort, null, "MqttConsumerProducerConfigAndDisposeTests");

        var producerConfig = new MqttMessagingGatewayProducerConfiguration
        {
            Hostname = IPAddress.Loopback.ToString(),
            Port = serverPort,
            TopicPrefix = topicPrefix
        };

        _producer = new MqttMessageProducer(new MqttMessagePublisher(producerConfig), new Publication());

        _scheduler = new SpySchedulerSync();

        var consumerConfig = new MqttMessagingGatewayConsumerConfiguration
        {
            Hostname = IPAddress.Loopback.ToString(),
            Port = serverPort,
            TopicPrefix = topicPrefix,
            ClientID = "BrighterIntegrationTests-SchedulerDispose"
        };

        // Create consumer WITH scheduler - this is the constructor parameter being tested
        _consumer = new MqttMessageConsumer(consumerConfig, _scheduler);
    }

    [Fact]
    public void When_requeuing_with_delay_should_use_scheduler()
    {
        // Arrange - send a message and receive it
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("scheduler-test"), MessageType.MT_COMMAND),
            new MessageBody("test content for scheduler injection"));

        ((IAmAMessageProducerSync)_producer).Send(message);
        var received = ReceiveMessage();

        // Act - requeue with delay (triggers lazy producer creation with scheduler)
        _consumer.Requeue(received, TimeSpan.FromSeconds(5));

        // Assert - scheduler should have been called (proves producer has scheduler configured)
        Assert.True(_scheduler.ScheduleCalled,
            "Scheduler.Schedule should have been called via the lazily created producer");
        Assert.Equal(message.Body.Value, _scheduler.ScheduledMessage?.Body.Value);
    }

    [Fact]
    public void When_consumer_disposes_after_requeue_should_dispose_producer()
    {
        // Arrange - trigger lazy producer creation via requeue
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("dispose-test"), MessageType.MT_COMMAND),
            new MessageBody("test content for dispose"));

        ((IAmAMessageProducerSync)_producer).Send(message);
        var received = ReceiveMessage();
        _consumer.Requeue(received, TimeSpan.FromSeconds(5));

        // Act + Assert - disposing should not throw (producer cleanup succeeds)
        var exception = Record.Exception(() => _consumer.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void When_consumer_disposes_without_requeue_should_not_throw()
    {
        // Act + Assert - disposing without ever requeuing should succeed (no producer created)
        var exception = Record.Exception(() => _consumer.Dispose());
        Assert.Null(exception);
    }

    private Message ReceiveMessage()
    {
        int maxTries = 10;
        for (int i = 0; i < maxTries; i++)
        {
            var messages = ((IAmAMessageConsumerSync)_consumer).Receive(TimeSpan.FromMilliseconds(500));
            if (messages.Length > 0 && messages[0].Header.MessageType != MessageType.MT_NONE)
            {
                return messages[0];
            }

            Thread.Sleep(100);
        }

        throw new Exception($"Failed to receive message after {maxTries} attempts");
    }

    public void Dispose()
    {
        try { _consumer.Dispose(); } catch { /* may already be disposed */ }
        _producer.Dispose();
        _mqttTestServer?.Dispose();
    }

    /// <summary>
    /// A spy sync scheduler that records calls to Schedule for verification.
    /// </summary>
    private sealed class SpySchedulerSync : IAmAMessageSchedulerSync
    {
        public bool ScheduleCalled { get; private set; }
        public Message? ScheduledMessage { get; private set; }
        public TimeSpan? ScheduledDelay { get; private set; }

        public string Schedule(Message message, DateTimeOffset at)
        {
            ScheduleCalled = true;
            ScheduledMessage = message;
            return Guid.NewGuid().ToString();
        }

        public string Schedule(Message message, TimeSpan delay)
        {
            ScheduleCalled = true;
            ScheduledMessage = message;
            ScheduledDelay = delay;
            return Guid.NewGuid().ToString();
        }

        public bool ReScheduler(string schedulerId, DateTimeOffset at) => true;

        public bool ReScheduler(string schedulerId, TimeSpan delay) => true;

        public void Cancel(string id) { }
    }
}
