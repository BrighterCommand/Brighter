using System;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.Kafka;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Proactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class KafkaProducerOversizedMessageTestsAsync : IDisposable, IAsyncDisposable
{
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;

    public KafkaProducerOversizedMessageTestsAsync()
    {
        _producerRegistry = new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Oversized Produce Test", BootStrapServers = new[] { "localhost:9092" }
            },
            [
                new KafkaPublication
                {
                    Topic = new RoutingKey(_topic),
                    NumPartitions = 1,
                    ReplicationFactor = 1,
                    MessageTimeoutMs = 2000,
                    RequestTimeoutMs = 2000,
                    MakeChannels = OnMissingChannel.Create
                }
            ]).CreateAsync().Result;
    }

    [Fact]
    public async Task When_an_async_produce_throws_should_warn_and_synthesize_not_persisted()
    {
        //Let the topic propagate in the broker
        await Task.Delay(500);

        var routingKey = new RoutingKey(_topic);

        // A body well over the client's default MessageMaxBytes (~1MB) makes ProduceAsync throw a
        // ProduceException with Error.Code = MsgSizeTooLarge — the failure path this test exercises.
        var body = new string('x', 2_000_000);
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_EVENT)
            {
                ContentType = new ContentType(MediaTypeNames.Text.Plain)
            },
            new MessageBody(body));

        var producerAsync = _producerRegistry.LookupAsyncBy(routingKey);
        var producerConfirm = (ISupportPublishConfirmation)producerAsync;

        var raised = new TaskCompletionSource<PublishConfirmationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        producerConfirm.OnMessagePublished += result => raised.TrySetResult(result);

        // The Warning is logged inside the awaited PublishMessageAsync (the ProduceException catch),
        // so it is captured within this correlation context.
        using var context = TestCorrelator.CreateContext();

        await producerAsync.SendAsync(message);

        // FR-7: the synthetic NotPersisted delivery result is still routed to the callback (it does
        // not bubble out of Send) — and it surfaces as a failed confirmation.
        var confirmation = await raised.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(confirmation.Success);

        // FR-6 / NFR-1: the swallowed ProduceException's reason and code were logged at Warning.
        var warnings = TestCorrelator.GetLogEventsFromCurrentContext()
            .Where(e => e.Level == LogEventLevel.Warning)
            .Select(e => e.RenderMessage())
            .ToList();
        Assert.Contains(warnings, m =>
            m.Contains("MsgSizeTooLarge", StringComparison.OrdinalIgnoreCase)
            || m.Contains("Message size too large", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose() => _producerRegistry.Dispose();

    public ValueTask DisposeAsync()
    {
        _producerRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}
