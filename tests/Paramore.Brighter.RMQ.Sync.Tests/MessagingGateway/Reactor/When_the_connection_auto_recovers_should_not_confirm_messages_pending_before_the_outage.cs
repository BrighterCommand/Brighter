using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;
using RabbitMQ.Client;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqAutoRecoveryConfirmationTests : IDisposable
{
    private const string ManagementApi = "http://localhost:15672/api/connections";

    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private readonly string _connectionName = $"bugfix-0056-{Guid.NewGuid()}";
    private readonly HttpClient _management = new();
    private readonly CoalescingConfirmsRmqProducer _messageProducer;

    public RmqAutoRecoveryConfirmationTests()
    {
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange"),
            Name = _connectionName
        };

        _messageProducer = new CoalescingConfirmsRmqProducer(rmqConnection);

        //we need a queue to avoid a discard
        new QueueFactory(rmqConnection, new ChannelName(Guid.NewGuid().ToString()), new RoutingKeys(_routingKey))
            .Create(TimeSpan.FromMilliseconds(1000));

        _management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("guest:guest")));
    }

    [Fact]
    public void When_the_connection_auto_recovers_should_not_confirm_messages_pending_before_the_outage()
    {
        //Arrange — two messages await confirmation when the broker force-closes the connection
        var confirmations = new List<PublishConfirmationResult>();
        _messageProducer.OnMessagePublished += confirmations.Add;

        _messageProducer.Send(NewMessage());
        _messageProducer.Send(NewMessage());

        var channel = (IModel)(object)_messageProducer.Confirms;
        CloseConnectionFromTheBroker();

        // nothing sends during the outage, so the client recovers the same channel rather than the gateway replacing it
        WaitUntil(() => channel.IsClosed, "the channel to close");
        WaitUntil(() => channel.IsOpen, "the client to recover the channel");
        Assert.Single(_messageProducer.Channels);
        Assert.Equal(1ul, channel.NextPublishSeqNo);

        // the recovered channel numbers its delivery tags from 1 again
        var sentAfterRecovery = new[] { NewMessage(), NewMessage() };
        foreach (var message in sentAfterRecovery)
            _messageProducer.Send(message);

        //Act — one frame on the recovered channel acks its delivery tags 1 to 2
        _messageProducer.Confirms.RaiseAck(deliveryTag: 2, multiple: true);

        //Assert — only the messages sent after recovery are confirmed
        Assert.Equal(sentAfterRecovery.Select(m => m.Id), confirmations.Select(c => c.MessageId));
    }

    private void CloseConnectionFromTheBroker()
    {
        // The management plugin lists a connection only after its next statistics interval
        string? brokerName = null;
        var seen = new List<string>();
        WaitUntil(() =>
        {
            using var listing = JsonDocument.Parse(_management.GetStringAsync(ManagementApi).GetAwaiter().GetResult());
            seen = listing.RootElement.EnumerateArray()
                .Select(c => c.GetProperty("client_properties").TryGetProperty("connection_name", out var name)
                    ? name.GetString() ?? string.Empty
                    : string.Empty)
                .ToList();
            brokerName = listing.RootElement.EnumerateArray()
                .Where(c => c.GetProperty("client_properties").TryGetProperty("connection_name", out var name)
                            && name.GetString() == _connectionName)
                .Select(c => c.GetProperty("name").GetString())
                .FirstOrDefault();
            return brokerName is not null;
        }, $"the broker to list connection {_connectionName}; it lists [{string.Join(", ", seen)}]");

        var response = _management.DeleteAsync($"{ManagementApi}/{Uri.EscapeDataString(brokerName!)}").GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
    }

    // The client recovers on its own thread, after a network recovery interval of 5 seconds
    private static void WaitUntil(Func<bool> condition, string description)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"Timed out waiting for {description}");
            Thread.Sleep(50);
        }
    }

    private Message NewMessage() => new(
        new MessageHeader(Id.Random(), _routingKey, MessageType.MT_COMMAND),
        new MessageBody("confirm me"));

    public void Dispose()
    {
        _messageProducer.Dispose();
        _management.Dispose();
    }
}
