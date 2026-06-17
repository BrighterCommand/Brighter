using System;
using System.Diagnostics;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Proactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class KafkaConfirmationTopicAndLinkTestsAsync : IDisposable, IAsyncDisposable
{
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;

    public KafkaConfirmationTopicAndLinkTestsAsync()
    {
        _producerRegistry = new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Confirmation Topic And Link Test", BootStrapServers = new[] { "localhost:9092" }
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
    public async Task When_a_kafka_confirmation_fires_should_carry_topic_and_link_from_message()
    {
        //Let the topic propagate in the broker
        await Task.Delay(500);

        var routingKey = new RoutingKey(_topic);

        // Establish an active publish span before the produce so the captured context is non-default.
        using var activitySource = new ActivitySource("brighter.test.kafka.confirmation");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        using var publishActivity = activitySource.StartActivity("publish");
        Assert.NotNull(publishActivity); // sampling must be active for the test to be meaningful
        var capturedContext = publishActivity.Context;

        // An oversized body forces the synthetic NotPersisted path. Its delivery report never sets
        // .Topic, so the confirmation's topic must come from message.Header.Topic, NOT report.Topic.
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

        // Act — send while publishActivity is Activity.Current; the producer captures the context
        // synchronously at the top of SendAsync, before the delivery-report closure runs.
        await producerAsync.SendAsync(message);

        // Assert — FR-2 (Kafka) / C-7 / C-8: even on the synthetic NotPersisted path the failed
        // confirmation carries the wire topic from message.Header.Topic and a link to the publish span.
        var confirmation = await raised.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(confirmation.Success);
        Assert.Equal(message.Header.Topic, confirmation.Topic);
        Assert.NotNull(confirmation.PublishSpanContext);
        Assert.Equal(capturedContext.TraceId, confirmation.PublishSpanContext.Value.TraceId);
        Assert.Equal(capturedContext.SpanId, confirmation.PublishSpanContext.Value.SpanId);
    }

    public void Dispose() => _producerRegistry.Dispose();

    public ValueTask DisposeAsync()
    {
        _producerRegistry.Dispose();
        return ValueTask.CompletedTask;
    }
}
