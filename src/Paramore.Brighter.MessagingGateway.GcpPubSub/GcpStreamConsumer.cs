using System.Threading.Channels;
using Google.Cloud.PubSub.V1;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// Manages the lifecycle of the Google Cloud Pub/Sub streaming consumer (<see cref="SubscriberClient"/>)
/// and funnels received messages into a local thread-safe channel for processing.
/// </summary>
public class GcpStreamConsumer(SubscriberClient client)
{
    private int _handlers;
    
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
    /// Starts the Pub/Sub streaming client if it is the first call, and begins reading messages into the channel.
    /// </summary>
    public void Start()
    {
        var res = Interlocked.Increment(ref _handlers);
        if (res > 1)
        {
            return;
        }
        
        client.StartAsync(new BrighterStreamHandler(_channel.Writer));
    }

    /// <summary>
    /// Stops the Pub/Sub streaming client and disposes of resources when the last outstanding handler calls this method.
    /// </summary>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    public async Task StopAsync()
    {
        var decrement = Interlocked.Decrement(ref _handlers);
        if (decrement == 0)
        {
            await client.StopAsync(CancellationToken.None);
            await client.DisposeAsync();
        }
    }
}


/// <summary>
/// An implementation of the Google Cloud Pub/Sub <see cref="SubscriptionHandler"/> 
/// that processes incoming <see cref="PubsubMessage"/>s and writes them to an internal 
/// channel for further consumption.
/// </summary>
public class BrighterStreamHandler(ChannelWriter<GcpStreamMessage> writer) :  SubscriptionHandler
{
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
            var streamMessage = new GcpStreamMessage(message);
            cancellationToken.Register(() => streamMessage.Cancel(cancellationToken));
            
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
