using System;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Proactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class KafkaNotPersistedConfirmationIdTestsAsync : IDisposable, IAsyncDisposable
{
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;

    public KafkaNotPersistedConfirmationIdTestsAsync()
    {
        _producerRegistry = new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka NotPersisted Id Test", BootStrapServers = new[] { "localhost:9092" }
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
    public async Task When_publish_results_not_persisted_should_raise_failure_with_id()
    {
        //Let the topic propagate in the broker
        await Task.Delay(500);

        var routingKey = new RoutingKey(_topic);
        var messageId = Guid.NewGuid().ToString();

        // An oversized body forces the synthetic NotPersisted path; its delivery report still carries
        // MESSAGE_ID in the report-level headers, which the failed confirmation must surface.
        var body = new string('x', 2_000_000);
        var message = new Message(
            new MessageHeader(messageId, routingKey, MessageType.MT_EVENT)
            {
                ContentType = new ContentType(MediaTypeNames.Text.Plain)
            },
            new MessageBody(body));

        var producerAsync = _producerRegistry.LookupAsyncBy(routingKey);
        var producerConfirm = (ISupportPublishConfirmation)producerAsync;

        var raised = new TaskCompletionSource<PublishConfirmationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        producerConfirm.OnMessagePublished += result => raised.TrySetResult(result);

        await producerAsync.SendAsync(message);

        // AC-7: the NotPersisted confirmation is a failure that carries the message id read from the
        // report-level headers (not Id.Empty, and not from result.Message.Headers).
        var confirmation = await raised.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(confirmation.Success);
        Assert.Equal(messageId, confirmation.MessageId.Value);
    }

    public void Dispose() => _producerRegistry.Dispose();

    public ValueTask DisposeAsync()
    {
        _producerRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}
