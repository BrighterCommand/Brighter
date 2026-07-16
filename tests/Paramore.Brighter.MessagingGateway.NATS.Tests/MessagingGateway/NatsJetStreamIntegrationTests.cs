#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NATS.Net;
using Paramore.Brighter.MessagingGateway.NATS;
using Shouldly;
using Xunit;

namespace Paramore.Brighter.MessagingGateway.NATS.Tests.MessagingGateway;

public class NatsJetStreamIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly string _streamName = $"BRIGHTER_TEST_{Guid.NewGuid():N}";
    private readonly string _subject = $"brighter.test.{Guid.NewGuid():N}";
    private NatsMessagingGatewayConfiguration _configuration = null!;
    private NatsClient _client = null!;

    public NatsJetStreamIntegrationTests()
    {
        var url = Environment.GetEnvironmentVariable("NATS_URL");
        if (!string.IsNullOrWhiteSpace(url))
        {
            _configuration = new NatsMessagingGatewayConfiguration { Urls = new[] { url } };
        }
    }

    public async Task InitializeAsync()
    {
        if (_configuration == null)
            return;

        _client = NatsConnectionFactory.CreateClient(_configuration);
        await _client.ConnectAsync();
        var js = _client.CreateJetStreamContext();
        try
        {
            await js.DeleteStreamAsync(_streamName);
        }
        catch
        {
            // ignore if stream does not exist
        }
    }

    public async Task DisposeAsync()
    {
        if (_client == null)
            return;

        try
        {
            var js = _client.CreateJetStreamContext();
            await js.DeleteStreamAsync(_streamName);
        }
        catch
        {
            // ignore cleanup failures
        }

        await _client.DisposeAsync();
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [NatsAvailableFact]
    public async Task When_publishing_and_consuming_via_jetstream_should_round_trip_message()
    {
        // Arrange
        var publication = new NatsPublication
        {
            Topic = new RoutingKey(_subject),
            StreamName = _streamName
        };
        var producer = new NatsMessageProducer(_configuration, publication);
        await producer.InitAsync();

        var messageId = Guid.NewGuid().ToString();
        var message = new Message(
            new MessageHeader(
                messageId: messageId,
                topic: new RoutingKey(_subject),
                messageType: MessageType.MT_EVENT)
            {
                Bag = { ["custom"] = "value" }
            },
            new MessageBody(Encoding.UTF8.GetBytes("round-trip-payload")));

        // Act
        await producer.SendAsync(message);

        var subscription = new NatsSubscription<MyCommand>(
            channelName: new ChannelName($"consumer-{Guid.NewGuid():N}"),
            routingKey: new RoutingKey(_subject),
            streamName: _streamName,
            pullTimeout: TimeSpan.FromSeconds(2));

        using var consumer = new NatsMessageConsumer(_configuration, subscription);

        Message[] received = Array.Empty<Message>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!cts.IsCancellationRequested)
        {
            received = consumer.Receive(TimeSpan.FromMilliseconds(500));
            if (received.Length > 0 && received[0].Header.MessageType != MessageType.MT_NONE)
                break;
        }

        // Assert
        received.Length.ShouldBeGreaterThan(0);
        var receivedMessage = received[0];
        receivedMessage.Header.MessageId.Value.ShouldBe(messageId);
        receivedMessage.Header.Topic.Value.ShouldBe(_subject);
        receivedMessage.Header.Bag["custom"].ShouldBe("value");
        Encoding.UTF8.GetString(receivedMessage.Body.Bytes).ShouldBe("round-trip-payload");

        await consumer.AcknowledgeAsync(receivedMessage);
    }
}
