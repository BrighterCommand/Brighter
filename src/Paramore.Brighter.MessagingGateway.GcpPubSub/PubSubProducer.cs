using System.Diagnostics;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The Google Cloud PubSub producer
/// </summary>
/// <param name="client">The <see cref="PublisherClient"/>.</param>
/// <param name="publication">The <see cref="PubSubPublication"/>.</param>
public class PubSubProducer(PublisherClient client, PubSubPublication publication)
    : IAmAMessageProducerAsync, IAmAMessageProducerSync
{
    /// <inheritdoc />
    public Publication Publication => publication;

    /// <inheritdoc />
    public Activity? Span { get; set; }

    /// <inheritdoc />
    public IAmAMessageScheduler? Scheduler { get; set; }

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
            await client.PublishAsync(pubSubMessage);
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

    private static void AddHeaders(MapField<string, string> headers, Message message)
    {
        headers.Add(HeaderNames.Id, message.Header.MessageId);
        headers.Add(HeaderNames.Topic, message.Header.Topic);
        headers.Add(HeaderNames.HandledCount, message.Header.HandledCount.ToString());
        headers.Add(HeaderNames.MessageType, message.Header.MessageType.ToString());
        headers.Add(HeaderNames.Timestamp, Convert.ToString(message.Header.TimeStamp)!);

        if (!string.IsNullOrEmpty(message.Header.ContentType))
        {
            headers.Add(HeaderNames.ContentType, message.Header.ContentType!);
        }

        if (!string.IsNullOrEmpty(message.Header.CorrelationId))
        {
            headers.Add(HeaderNames.CorrelationId, message.Header.CorrelationId);
        }

        if (!string.IsNullOrEmpty(message.Header.ReplyTo))
        {
            headers.Add(HeaderNames.ReplyTo, message.Header.ReplyTo!);
        }

        if (!string.IsNullOrEmpty(message.Header.Subject))
        {
            headers.Add(HeaderNames.Subject, message.Header.Subject!);
        }

        message.Header.Bag.Each(header =>
        {
            if (!headers.ContainsKey(header.Key))
            {
                headers.Add(header.Key, header.Value.ToString()!);
            }
        });
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
        client.DisposeAsync()
            .GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
        => await client.DisposeAsync();
}
