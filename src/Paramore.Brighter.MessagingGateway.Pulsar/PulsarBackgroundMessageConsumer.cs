using System.Buffers;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DotPulsar.Abstractions;

namespace Paramore.Brighter.MessagingGateway.Pulsar;

public sealed class PulsarBackgroundMessageConsumer(int maxLenght, IConsumer<ReadOnlySequence<byte>> consumer)
{
    private int _total;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly Channel<IMessage<ReadOnlySequence<byte>>> _channel = System.Threading.Channels.Channel.CreateBounded<IMessage<ReadOnlySequence<byte>>>(new BoundedChannelOptions(maxLenght)
    {
        SingleReader = false, SingleWriter = true
    });
    
    public ChannelReader<IMessage<ReadOnlySequence<byte>>> Reader => _channel.Reader;
    
    public IConsumer<ReadOnlySequence<byte>> Consumer => consumer;

    public void Start()
    {
        var total = Interlocked.Increment(ref _total);
        if (total == 1)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _ = ExecuteAsync(_cancellationTokenSource.Token);
        }
    }

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

    public void Stop()
    {
        var total = Interlocked.Decrement(ref _total);
        if (total == 0 && _cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
        }
    }
}
