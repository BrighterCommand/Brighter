using System.Diagnostics;
using System.Runtime.CompilerServices;
using Google.Api.Gax.Grpc;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The Google Cloud PubSub producer
/// </summary>
public class GcpMessageProducer : IAmAMessageProducerAsync, IAmAMessageProducerSync, IAmABulkMessageProducerAsync
{
    /// <inheritdoc />
    public Publication Publication => _publication;

    /// <inheritdoc />
    public Activity? Span { get; set; }

    /// <inheritdoc />
    public IAmAMessageScheduler? Scheduler { get; set; }

    private readonly TopicName _topicName;
    private readonly GcpMessagingGatewayConnection _connection;
    private readonly GcpPublication _publication;

    /// <summary>
    /// The Google Cloud PubSub producer
    /// </summary>
    public GcpMessageProducer(GcpMessagingGatewayConnection connection, GcpPublication publication)
    {
        _connection = connection;
        _publication = publication;
        if (_publication.TopicAttributes != null && TopicName.TryParse(_publication.TopicAttributes.Name, out var topicName))
        {
            _topicName = topicName;
        }
        else
        {
            _topicName = TopicName.FromProjectTopic(publication.TopicAttributes?.ProjectId ?? connection.ProjectId,
                publication.TopicAttributes?.Name ?? publication.Topic!.Value);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Id[]> SendAsync(IEnumerable<Message> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var msg = messages.ToArray();
        if (msg.Length == 0)
        {
            yield break;
        }

        var client = await _connection.CreatePublisherServiceApiClientAsync();
        
        foreach (var chuck in msg.Chunk(_publication.BatchSize))
        {
            var pubSubMessages = new List<PubsubMessage>();
            foreach (var message in chuck)
            {
                pubSubMessages.Add(Parser.ToPubSubMessage(message));
                BrighterTracer.WriteProducerEvent(Span, MessagingSystem.PubSub, message);
            }

            await client.PublishAsync(
                new PublishRequest { TopicAsTopicName = _topicName, Messages = { pubSubMessages } },
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
        var client = await _connection.CreatePublisherServiceApiClientAsync();

        if (delay == null || delay == TimeSpan.Zero)
        {
            var pubSubMessage = Parser.ToPubSubMessage(message);
            BrighterTracer.WriteProducerEvent(Span, MessagingSystem.PubSub, message);
            await client.PublishAsync(
                new PublishRequest { TopicAsTopicName = _topicName, Messages = { pubSubMessage } },
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
    {
        var client = _connection.CreatePublisherServiceApiClient();

        if (delay == null || delay == TimeSpan.Zero)
        {
            var pubSubMessage = Parser.ToPubSubMessage(message);
            BrighterTracer.WriteProducerEvent(Span, MessagingSystem.PubSub, message);
            client.Publish(new PublishRequest { TopicAsTopicName = _topicName, Messages = { pubSubMessage } });
        }
        else if (Scheduler is IAmAMessageSchedulerSync scheduler)
        {
            scheduler.Schedule(message, delay.Value);
        }
        else
        {
            throw new InvalidOperationException("Scheduler must be an IAmAMessageSchedulerAsync");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => new();
}
