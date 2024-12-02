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
using System.Threading.Tasks;

namespace Paramore.Brighter.Mediator;

/// <summary>
/// Represents a channel for job processing in a workflow.
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
public interface IAmAJobChannel<TData>
{
    /// <summary>
    /// Enqueues a job to the channel.
    /// </summary>
    /// <param name="job">The job to enqueue.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous enqueue operation.</returns>
    Task EnqueueJobAsync(Job<TData> job, CancellationToken cancellationToken = default(CancellationToken));

    /// <summary>
    /// Dequeues a job from the channel.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>A task that represents the asynchronous dequeue operation. The task result contains the dequeued job.</returns>
    Task<Job<TData>?> DequeueJobAsync(CancellationToken cancellationToken = default(CancellationToken));

    /// <summary>
    /// Streams jobs from the channel.
    /// </summary>
    /// <returns>An asynchronous enumerable of jobs.</returns>
    IAsyncEnumerable<Job<TData>> Stream();

    /// <summary>
    /// Determines whether the channel is closed.
    /// </summary>
    /// <returns><c>true</c> if the channel is closed; otherwise, <c>false</c>.</returns>
    bool IsClosed();
}
