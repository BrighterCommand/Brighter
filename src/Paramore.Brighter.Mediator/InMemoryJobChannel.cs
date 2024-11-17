#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Paramore.Brighter.Mediator;

public enum FullChannelStrategy
{
    Wait,
    Drop
}    


public class InMemoryJobChannel<TData> : IAmAJobChannel<TData>
{
    private readonly Channel<Job<TData>> _channel;

    public InMemoryJobChannel(int boundedCapacity = 100, FullChannelStrategy fullChannelStrategy = FullChannelStrategy.Wait)
    {
        if (boundedCapacity <= 0)
            throw new System.ArgumentOutOfRangeException(nameof(boundedCapacity), "Bounded capacity must be greater than 0");
        
        _channel = System.Threading.Channels.Channel.CreateBounded<Job<TData>>(new BoundedChannelOptions(boundedCapacity)
        {          
            SingleWriter = true,
            SingleReader = false,
            AllowSynchronousContinuations = true,
            FullMode = fullChannelStrategy == FullChannelStrategy.Wait ? 
                BoundedChannelFullMode.Wait : 
                BoundedChannelFullMode.DropOldest
        });
    }

    /// <summary>
    /// Dequeues a job from the channel.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>A task that represents the asynchronous dequeue operation. The task result contains the dequeued job.</returns>
    public async Task<Job<TData>> DequeueJobAsync(CancellationToken cancellationToken)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }

    /// <summary>
    /// Enqueues a job to the channel.
    /// </summary>
    /// <param name="job">The job to enqueue.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous enqueue operation.</returns>
    public async Task EnqueueJobAsync(Job<TData> job, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(job, cancellationToken);
    }

    /// <summary>
    /// Determines whether the channel is closed.
    /// </summary>
    /// <returns><c>true</c> if the channel is closed; otherwise, <c>false</c>.</returns>
    public bool IsClosed()
    {
        return _channel.Reader.Completion.IsCompleted;
    } 
    
    /// <summary>
    /// Streams jobs from the channel.
    /// </summary>
    /// <returns>An asynchronous enumerable of jobs.</returns>
    public IAsyncEnumerable<Job<TData>> Stream()
    { 
        return _channel.Reader.ReadAllAsync();
    }
}
