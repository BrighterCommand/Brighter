using System.Diagnostics;
using System.Runtime.CompilerServices;
using Google.Api.Gax.Grpc;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The Google Cloud PubSub producer
/// </summary>
/// <param name="client">The <see cref="PublisherClient"/>.</param>
/// <param name="publication">The <see cref="PubSubPublication"/>.</param>
public class PubSubProducer(
    PublisherServiceApiClient client,
    TopicName topicName,
    PubSubPublication publication)
    : IAmAMessageProducerAsync, IAmAMessageProducerSync, IAmABulkMessageProducerAsync
{
    /// <inheritdoc />
    public Publication Publication => publication;

    /// <inheritdoc />
    public Activity? Span { get; set; }

    /// <inheritdoc />
    public IAmAMessageScheduler? Scheduler { get; set; }

    /// <inheritdoc />
    public async IAsyncEnumerable<string[]> SendAsync(IEnumerable<Message> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var msg = messages.ToArray();
        if (msg.Length == 0)
        {
            yield break;
        }

        foreach (var chuck in msg.Chunk(publication.BatchSize))
        {
            var pubSubMessages = new List<PubsubMessage>();
            foreach (var message in chuck)
            {
                pubSubMessages.Add(Parser.ToPubSubMessage(message));
                BrighterTracer.WriteProducerEvent(Span, MessagingSystem.PubSub, message);
            }

            await client.PublishAsync(
                new PublishRequest { TopicAsTopicName = topicName, Messages = { pubSubMessages } },
                CallSettings.FromCancellationToken(cancellationToken));

            yield return chuck.Select(x => x.Id).ToArray();

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <inheritdoc />
    public Task SendAsync(Message message, CancellationToken cancellationToken = default)
        => SendWithDelayAsync(message, null, cancellationToken);

    /// <inheritdoc />
    public async Task SendWithDelayAsync(Message message, TimeSpan? delay,
        CancellationToken cancellationToken = default)
    {
        if (delay == null || delay == TimeSpan.Zero)
        {
            var pubSubMessage = Parser.ToPubSubMessage(message);
            BrighterTracer.WriteProducerEvent(Span, MessagingSystem.PubSub, message);
            await client.PublishAsync(new PublishRequest { TopicAsTopicName = topicName, Messages = { pubSubMessage } },
                CallSettings.FromCancellationToken(cancellationToken));
        }
        else if (Scheduler is IAmAMessageSchedulerAsync scheduler)
        {
            await scheduler.ScheduleAsync(message, delay.Value, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("Scheduler must be an IAmAMessageSchedulerAsync");
        }
    }

    /// <inheritdoc />
    public void Send(Message message)
        => SendWithDelay(message, null);

    /// <inheritdoc />
    public void SendWithDelay(Message message, TimeSpan? delay)
        => BrighterAsyncContext.Run(async () => await SendWithDelayAsync(message, delay));

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => new();
}
