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
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.RMQ.Async.Tests.TestDoubles;
using RabbitMQ.Client;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqPurgeBufferedMessagesTests
{
    [Theory]
    [InlineData(false, false, QueueType.Classic, false)]
    [InlineData(true, false, QueueType.Classic, false)]
    [InlineData(false, true, QueueType.Classic, false)]
    [InlineData(true, true, QueueType.Classic, false)]
    [InlineData(false, false, QueueType.Quorum, false)]
    [InlineData(true, false, QueueType.Quorum, false)]
    [InlineData(false, true, QueueType.Quorum, false)]
    [InlineData(true, true, QueueType.Quorum, false)]
    [InlineData(true, false, QueueType.Classic, true)]
    [InlineData(true, false, QueueType.Quorum, true)]
    public async Task When_purging_with_prefetched_messages_should_discard_buffers_and_preserve_in_flight_messages(
        bool bufferInBrighterChannel, bool recoverConnection, QueueType queueType, bool failAcknowledgement)
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
        await using var adminConnection = await factory.CreateConnectionAsync();
        await using var adminChannel = await adminConnection.CreateChannelAsync();
        string? failure = null;
        await using var consumer = failAcknowledgement
            ? new CleanupFailureRmqConsumer(connection, queueName, routingKey, () => failure, batchSize: 3, queueType: queueType)
            : new RmqMessageConsumer(connection, queueName, routingKey, isDurable: true, batchSize: 3, queueType: queueType);
        await using var channel = new ChannelAsync(queueName, routingKey, consumer, maxQueueLength: 3);
        await using var producer = new RmqMessageProducer(connection);

        try
        {
            await new QueueFactory(connection, queueName, new RoutingKeys(routingKey), isDurable: true, queueType: queueType).CreateAsync();
            for (var i = 0; i < 4; i++)
            {
                await producer.SendAsync(createMessage(routingKey));
            }

            await waitForReadyMessagesAsync(adminChannel, queueName, 4);
            Assert.True(Assert.Single(await consumer.ReceiveAsync(TimeSpan.Zero)).IsEmpty);
            await waitForReadyMessagesAsync(adminChannel, queueName, 1);
            if (recoverConnection)
            {
                await recoverConnectionAsync(connection, factory);
                await waitForReadyMessagesAsync(adminChannel, queueName, 1);
            }

            Message? inFlight = null;
            if (bufferInBrighterChannel)
            {
                inFlight = await channel.ReceiveAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(MessageType.MT_COMMAND, inFlight.Header.MessageType);
            }

            // Act
            if (failAcknowledgement)
            {
                failure = "BasicAckAsync";
                await Assert.ThrowsAsync<InvalidOperationException>(() => channel.PurgeAsync());
                failure = null;
            }

            for (var i = 0; i < 2; i++)
            {
                if (bufferInBrighterChannel)
                {
                    await channel.PurgeAsync();
                }
                else
                {
                    await consumer.PurgeAsync();
                }
            }

            // Assert
            Assert.Equal(MessageType.MT_NONE,
                (await channel.ReceiveAsync(TimeSpan.FromMilliseconds(500))).Header.MessageType);

            if (inFlight != null)
            {
                await channel.NackAsync(inFlight);
                var redelivered = await channel.ReceiveAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(inFlight.Id, redelivered.Id);
                await channel.AcknowledgeAsync(redelivered);
            }

            var fresh = createMessage(routingKey);
            await producer.SendAsync(fresh);
            var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(fresh.Id, received.Id);
            await channel.AcknowledgeAsync(received);
            await channel.DisposeAsync();

            await using var reconnected = new RmqMessageConsumer(connection, queueName, routingKey,
                isDurable: true, batchSize: 3, queueType: queueType);
            var afterReconnect = Assert.Single(await reconnected.ReceiveAsync(TimeSpan.FromSeconds(1)));
            Assert.Equal(MessageType.MT_NONE, afterReconnect.Header.MessageType);
        }
        finally
        {
            await channel.DisposeAsync();
            await adminChannel.QueueDeleteAsync(queueName.Value, ifUnused: false, ifEmpty: false);
        }
    }

    private static async Task recoverConnectionAsync(RmqMessagingGatewayConnection connection, ConnectionFactory factory)
    {
        var pool = new RmqMessageGatewayConnectionPool(connection.Name, connection.Heartbeat);
        var shared = await pool.GetConnectionAsync(factory);
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

    private static async Task waitForReadyMessagesAsync(IChannel channel, ChannelName queueName, uint expected)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            var queue = await channel.QueueDeclarePassiveAsync(queueName.Value);
            if (queue.MessageCount == expected)
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail($"The broker did not reach {expected} ready messages before the timeout.");
    }
}
