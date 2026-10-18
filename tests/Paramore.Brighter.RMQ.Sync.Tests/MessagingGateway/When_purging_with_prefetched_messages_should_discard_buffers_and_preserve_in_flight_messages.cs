#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

#nullable enable

using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;
using RabbitMQ.Client;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqPurgeBufferedMessagesTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task When_purging_with_prefetched_messages_should_discard_buffers_and_preserve_in_flight_messages(
        bool bufferInBrighterChannel, bool recoverConnection, bool failAcknowledgement)
    {
        // Arrange
        var queueName = new ChannelName(Guid.NewGuid().ToString());
        var routingKey = new RoutingKey(queueName.Value);
        var connection = new RmqMessagingGatewayConnection
        {
            Name = queueName.Value,
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };
        var factory = new ConnectionFactory { Uri = connection.AmpqUri.Uri };
        using var adminConnection = factory.CreateConnection();
        using var adminChannel = adminConnection.CreateModel();
        string? failure = null;
        using var consumer = failAcknowledgement
            ? new CleanupFailureRmqConsumer(connection, queueName, routingKey, () => failure, batchSize: 3)
            : new RmqMessageConsumer(connection, queueName, routingKey, isDurable: true, batchSize: 3);
        using var channel = new Channel(queueName, routingKey, consumer, maxQueueLength: 3);
        using var producer = new RmqMessageProducer(connection);

        try
        {
            new QueueFactory(connection, queueName, new RoutingKeys(routingKey), isDurable: true).Create(TimeSpan.Zero);
            for (var i = 0; i < 4; i++)
            {
                producer.Send(createMessage(routingKey));
            }

            waitForReadyMessages(adminChannel, queueName, 4);
            Assert.True(Assert.Single(consumer.Receive(TimeSpan.Zero)).IsEmpty);
            waitForReadyMessages(adminChannel, queueName, 1);
            if (recoverConnection)
            {
                await recoverConnectionAsync(connection, factory);
                waitForReadyMessages(adminChannel, queueName, 1);
            }

            Message? inFlight = null;
            if (bufferInBrighterChannel)
            {
                inFlight = channel.Receive(TimeSpan.FromSeconds(5));
                Assert.Equal(MessageType.MT_COMMAND, inFlight.Header.MessageType);
            }

            // Act
            if (failAcknowledgement)
            {
                failure = "BasicAck";
                Assert.Throws<InvalidOperationException>(() => channel.Purge());
                failure = null;
            }

            for (var i = 0; i < 2; i++)
            {
                if (bufferInBrighterChannel)
                {
                    channel.Purge();
                }
                else
                {
                    consumer.Purge();
                }
            }

            // Assert
            Assert.Equal(MessageType.MT_NONE,
                (channel.Receive(TimeSpan.FromMilliseconds(500))).Header.MessageType);

            if (inFlight != null)
            {
                channel.Nack(inFlight);
                var redelivered = channel.Receive(TimeSpan.FromSeconds(5));
                Assert.Equal(inFlight.Id, redelivered.Id);
                channel.Acknowledge(redelivered);
            }

            var fresh = createMessage(routingKey);
            producer.Send(fresh);
            var received = channel.Receive(TimeSpan.FromSeconds(5));
            Assert.Equal(fresh.Id, received.Id);
            channel.Acknowledge(received);
            channel.Dispose();

            using var reconnected = new RmqMessageConsumer(connection, queueName, routingKey,
                isDurable: true, batchSize: 3);
            var afterReconnect = Assert.Single(reconnected.Receive(TimeSpan.FromSeconds(1)));
            Assert.Equal(MessageType.MT_NONE, afterReconnect.Header.MessageType);
        }
        finally
        {
            channel.Dispose();
            adminChannel.QueueDelete(queueName.Value, ifUnused: false, ifEmpty: false);
        }
    }

    private static async Task recoverConnectionAsync(RmqMessagingGatewayConnection connection, ConnectionFactory factory)
    {
        var pool = new RmqMessageGatewayConnectionPool(connection.Name, connection.Heartbeat);
        var shared = pool.GetConnection(factory);
        using var management = new HttpClient { BaseAddress = new Uri("http://localhost:15672/api/") };
        management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("guest:guest")));
        var timeout = Stopwatch.StartNew();
        string? brokerName = null;
        while (brokerName == null && timeout.Elapsed < TimeSpan.FromSeconds(30))
        {
            using var listing = JsonDocument.Parse(await management.GetStringAsync("connections"));
            brokerName = listing.RootElement.EnumerateArray()
                .Where(c => c.GetProperty("client_properties").TryGetProperty("connection_name", out var name)
                    && name.GetString() == connection.Name)
                .Select(c => c.GetProperty("name").GetString())
                .FirstOrDefault();
            if (brokerName == null)
            {
                await Task.Delay(20);
            }
        }

        Assert.NotNull(brokerName);
        using var response = await management.DeleteAsync($"connections/{Uri.EscapeDataString(brokerName)}");
        response.EnsureSuccessStatusCode();
        timeout.Restart();
        while (shared.IsOpen && timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(20);
        }

        Assert.False(shared.IsOpen);
        while (!shared.IsOpen && timeout.Elapsed < TimeSpan.FromSeconds(30))
        {
            await Task.Delay(20);
        }

        Assert.True(shared.IsOpen);
    }

    private static Message createMessage(RoutingKey routingKey) =>
        new(new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND), new MessageBody("purge test"));

    private static void waitForReadyMessages(IModel channel, ChannelName queueName, uint expected)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            var queue = channel.QueueDeclarePassive(queueName.Value);
            if (queue.MessageCount == expected)
            {
                return;
            }

            Thread.Sleep(20);
        }

        Assert.Fail($"The broker did not reach {expected} ready messages before the timeout.");
    }
}
