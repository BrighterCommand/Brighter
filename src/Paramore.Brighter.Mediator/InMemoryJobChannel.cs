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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Mediator;

/// <summary>
/// Specifies the strategy to use when the channel is full.
/// </summary>
public enum FullChannelStrategy
{
    /// <summary>
    /// Wait for space to become available in the channel.
    /// </summary>
    Wait,
    
    /// <summary>
    /// Drop the oldest item in the channel to make space.
    /// </summary>
    Drop
}

/// <summary>
/// Represents an in-memory job channel for processing jobs.
/// </summary>
/// <typeparam name="TData">The type of the job data.</typeparam>
public class InMemoryJobChannel<TData> : IAmAJobChannel<TData>
{
    private readonly Channel<Job<TData>> _channel;
    
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<InMemoryJobChannel<TData>>();

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryJobChannel{TData}"/> class.
    /// </summary>
    /// <param name="boundedCapacity">The maximum number of jobs the channel can hold.</param>
    /// <param name="fullChannelStrategy">The strategy to use when the channel is full.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the bounded capacity is less than or equal to 0.</exception>
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
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous dequeue operation. The task result contains the dequeued job.</returns>
    public async Task<Job<TData>?> DequeueJobAsync(CancellationToken cancellationToken = default(CancellationToken))
    {                              
        Job<TData>? item = null;
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
            while (_channel.Reader.TryRead(out item)) 
                return item;

        return item;
    }

    /// <summary>
    /// Enqueues a job to the channel.
    /// </summary>
    /// <param name="job">The job to enqueue.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous enqueue operation.</returns>
    public async Task EnqueueJobAsync(Job<TData> job, CancellationToken cancellationToken = default(CancellationToken))
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
    /// This is mainly useful for help with testing, to stop the channel
    /// </summary>
    public void Stop()
    {
        _channel.Writer.Complete();
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
