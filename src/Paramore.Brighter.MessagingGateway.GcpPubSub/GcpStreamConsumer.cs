using System.Threading.Channels;
using Google.Cloud.PubSub.V1;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// Manages the lifecycle of the Google Cloud Pub/Sub streaming consumer (<see cref="SubscriberClient"/>)
/// and funnels received messages into a local thread-safe channel for processing.
/// </summary>
public class GcpStreamConsumer(SubscriberClient client)
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<GcpStreamConsumer>();
    private readonly object _lock = new();
    private int _handlers;
    private bool _stopped;
    private readonly GcpPurgeWatermark _purgeWatermark = new();

    internal SemaphoreSlim PurgeLock { get; } = new(1, 1);

    private readonly Channel<GcpStreamMessage> _channel =
        System.Threading.Channels.Channel.CreateUnbounded<GcpStreamMessage>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false,
        });

    /// <summary>
    /// Gets the channel reader used by message consumers to asynchronously read received Pub/Sub messages.
    /// </summary>
    public ChannelReader<GcpStreamMessage> Reader => _channel.Reader;

    /// <summary>
    /// Registers a handler, starting the Pub/Sub streaming client on the first registration so that it begins
    /// reading messages into the channel.
    /// </summary>
    /// <remarks>
    /// A <see cref="SubscriberClient"/> can only be started once, so once the last handler has stopped this
    /// consumer it refuses further handlers; the caller must create a new consumer instead.
    /// </remarks>
    /// <returns><c>true</c> if the handler was registered; <c>false</c> if this consumer has already stopped.</returns>
    public bool TryStart()
    {
        lock (_lock)
        {
            if (_stopped)
            {
                return false;
            }

            _handlers++;
            if (_handlers == 1)
            {
                client.StartAsync(new BrighterStreamHandler(_channel.Writer) { IsPurged = IsPurged });
            }

            return true;
        }
    }

    /// <summary>
    /// Stops the Pub/Sub streaming client and disposes of resources when the last outstanding handler calls this method.
    /// </summary>
    /// <remarks>
    /// Stops with <see cref="SubscriberClient.ShutdownMode.NackImmediately"/>: a message already delivered to a
    /// callback but not settled by Brighter (buffered unread in the channel, or received and never acknowledged,
    /// rejected or requeued) is nacked for redelivery. <see cref="SubscriberClient.ShutdownMode.WaitForProcessing"/>
    /// would wait on such a message until the client's own timeout, by default about an hour.
    /// Cancellation reported by the client during shutdown is logged and does not escape this method.
    /// Client disposal is always attempted; other failures propagate.
    /// </remarks>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    public async Task StopAsync()
    {
        bool lastHandler;
        lock (_lock)
        {
            _handlers--;
            lastHandler = _handlers == 0;
            if (lastHandler)
            {
                _stopped = true;
            }
        }

        if (lastHandler)
        {
            try
            {
                await client.StopAsync(new SubscriberClient.ShutdownOptions { Mode = SubscriberClient.ShutdownMode.NackImmediately }, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                s_logger.LogWarning(ex, "GcpStreamConsumer: shutdown was canceled; unsettled messages may be redelivered");
            }
            finally
            {
                await DisposeClientAsync().ConfigureAwait(false);
            }
        }
    }

    private async ValueTask DisposeClientAsync()
    {
        try
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            s_logger.LogDebug(ex, "GcpStreamConsumer: disposal completed with shutdown cancellation");
        }
    }

    /// <summary>
    /// Removes messages already buffered in the local channel, acknowledging each one.
    /// </summary>
    /// <remarks>
    /// Acknowledges buffered deliveries to release their flow-control slots. The drain is bounded by the
    /// initial buffer count so concurrent arrivals cannot keep it running indefinitely. This method does
    /// not seek the service subscription or establish a cutoff for late deliveries.
    /// </remarks>
    public void PurgeBuffered()
    {
        var bufferedCount = _channel.Reader.Count;
        for (var i = 0; i < bufferedCount && _channel.Reader.TryRead(out var message); i++)
        {
            message.Accepted();
        }
    }

    internal void PurgeBuffered(Timestamp purgeBefore)
    {
        _purgeWatermark.Advance(purgeBefore);
        PurgeBuffered();
    }

    internal bool IsPurged(PubsubMessage message) => _purgeWatermark.Includes(message);
}


/// <summary>
/// An implementation of the Google Cloud Pub/Sub <see cref="SubscriptionHandler"/> 
/// that processes incoming <see cref="PubsubMessage"/>s and writes them to an internal 
/// channel for further consumption.
/// </summary>
public class BrighterStreamHandler(ChannelWriter<GcpStreamMessage> writer) : SubscriptionHandler
{
    internal Func<PubsubMessage, bool>? IsPurged { get; init; }

    /// <summary>
    /// The handler invoked by the Pub/Sub client whenever a new message is received. 
    /// It wraps the message in a <see cref="GcpStreamMessage"/> and waits asynchronously 
    /// for the message to be processed (ACK/NACK) by the consumer.
    /// </summary>
    /// <param name="message">The raw Pub/Sub message received from the service.</param>
    /// <param name="cancellationToken">A cancellation token indicating that the streaming pull has been canceled.</param>
    /// <returns>A task that returns the final reply (Ack or Nack) to the Pub/Sub service.</returns>
    public override async Task<SubscriberClient.Reply> HandleMessage(PubsubMessage message, CancellationToken cancellationToken)
    {
        try
        {
            if (IsPurged?.Invoke(message) == true)
            {
                return SubscriberClient.Reply.Ack;
            }

            var streamMessage = new GcpStreamMessage(message);
            // The token lives as long as the client, so the registration must go once the message is settled,
            // or the token keeps every message the client ever delivers reachable
            using var registration = cancellationToken.Register(() => streamMessage.Cancel(cancellationToken));

            await writer.WriteAsync(streamMessage, cancellationToken);
            return await streamMessage.WaitForCompleteAsync();
        }
        catch (OperationCanceledException)
        {
            return SubscriberClient.Reply.Nack;
        }
    }
}

/// <summary>
/// Represents a Pub/Sub message currently in flight, acting as a receipt handle for the consumer. 
/// It uses a <see cref="TaskCompletionSource{TResult}"/> to block the underlying streaming 
/// handler until the message is explicitly ACKed, NACKed, or canceled by the consumer thread.
/// </summary>
public record GcpStreamMessage(PubsubMessage Message)
{
    private readonly TaskCompletionSource<SubscriberClient.Reply> _tcs = new();

    /// <summary>
    /// Sets the cancellation token on the internal TaskCompletionSource. 
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to set.</param>
    public void SetCancellationToken(CancellationToken cancellationToken)
    {
#if NETSTANDARD2_0
        _tcs.SetCanceled();
#else
        _tcs.SetCanceled(cancellationToken);
#endif
    }

    /// <summary>
    /// Waits asynchronously until the message is either acknowledged (<see cref="Accepted"/>), 
    /// rejected (<see cref="Reject"/>), or canceled.
    /// </summary>
    /// <returns>A task that completes with the final <see cref="SubscriberClient.Reply"/> status.</returns>
    public Task<SubscriberClient.Reply> WaitForCompleteAsync()
    {
        return _tcs.Task;
    }

    /// <summary>
    /// Signals that the message was successfully processed and should be acknowledged (ACK) 
    /// to the Pub/Sub service.
    /// </summary>
    public void Accepted()
    {
        _tcs.TrySetResult(SubscriberClient.Reply.Ack);
    }

    /// <summary>
    /// Signals that the message failed processing and should be negatively acknowledged (NACK) 
    /// to the Pub/Sub service for redelivery.
    /// </summary>
    public void Reject()
    {
        _tcs.TrySetResult(SubscriberClient.Reply.Nack);
    }

    /// <summary>
    /// Signals that the message processing was canceled due to a timeout or shutdown.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token that triggered the cancellation.</param>
    public void Cancel(CancellationToken cancellationToken)
    {
        _tcs.TrySetCanceled(cancellationToken);
    }

    /// <summary>
    /// Checks if the underlying task is still running and has not been canceled.
    /// </summary>
    public bool CanProcess => !_tcs.Task.IsCanceled;
}
