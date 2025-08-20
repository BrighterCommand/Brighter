using System.Buffers;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DotPulsar.Abstractions;

namespace Paramore.Brighter.MessagingGateway.Pulsar;

/// <summary>
/// Background message consumer for Apache Pulsar that buffers messages in a bounded channel.
/// </summary>
/// <remarks>
/// This class manages a background message consumption loop that:
/// <list type="bullet">
///   <item><description>Receives messages from Pulsar using an <see cref="IConsumer{T}"/></description></item>
///   <item><description>Writes messages to a bounded channel for consumption by other components</description></item>
///   <item><description>Implements reference counting for safe start/stop operations</description></item>
/// </list>
/// 
/// The consumer uses a fire-and-forget pattern where the consumption loop runs independently once started.
/// </remarks>
/// <param name="maxLenght">Maximum number of messages to buffer in the channel</param>
/// <param name="consumer">Pulsar message consumer implementation</param>
public sealed class PulsarBackgroundMessageConsumer(int maxLenght, IConsumer<ReadOnlySequence<byte>> consumer)
{
    private int _total;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly Channel<IMessage<ReadOnlySequence<byte>>> _channel = System.Threading.Channels.Channel.CreateBounded<IMessage<ReadOnlySequence<byte>>>(new BoundedChannelOptions(maxLenght)
    {
        SingleReader = false, SingleWriter = true
    });
    
    /// <summary>
    /// Provides read access to the message channel
    /// </summary>
    public ChannelReader<IMessage<ReadOnlySequence<byte>>> Reader => _channel.Reader;
    
    /// <summary>
    /// Gets the underlying Pulsar consumer instance
    /// </summary>
    public IConsumer<ReadOnlySequence<byte>> Consumer => consumer;

    /// <summary>
    /// Starts the background message consumption loop
    /// </summary>
    /// <remarks>
    /// Implements reference counting:
    /// <list type="bullet">
    ///   <item><description>First call starts the background loop</description></item>
    ///   <item><description>Subsequent calls increment the reference count but don't start additional loops</description></item>
    /// </list>
    /// </remarks>
    public void Start()
    {
        var total = Interlocked.Increment(ref _total);
        if (total == 1)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _ = ExecuteAsync(_cancellationTokenSource.Token);
        }
    }

    /// <summary>
    /// Background message consumption loop
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the loop</param>
    /// <remarks>
    /// Continuously performs:
    /// <list type="number">
    ///   <item><description>Receive message from Pulsar</description></item>
    ///   <item><description>Write message to output channel</description></item>
    ///   <item><description>Wait for channel write availability</description></item>
    /// </list>
    /// 
    /// <para>Errors during message reception are silently ignored to maintain loop continuity.</para>
    /// </remarks>
    private async Task ExecuteAsync(CancellationToken  cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var message = await consumer.Receive(cancellationToken);
                if (message is null)
                {
                    continue;
                }
                
                await _channel.Writer.WriteAsync(message, cancellationToken);
                await _channel.Writer.WaitToWriteAsync(cancellationToken);
            }
            catch
            {
                // Ignoring any errors
            }
        }
    }

    /// <summary>
    /// Stops the background message consumption loop
    /// </summary>
    /// <remarks>
    /// Implements reference counting:
    /// <list type="bullet">
    ///   <item><description>Decrements the reference count</description></item>
    ///   <item><description>Stops the loop when reference count reaches zero</description></item>
    /// </list>
    /// 
    /// Safe to call multiple times - only the last call that brings the count to zero will stop the loop.
    /// </remarks>
    public void Stop()
    {
        var total = Interlocked.Decrement(ref _total);
        if (total == 0 && _cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
        }
    }
}
