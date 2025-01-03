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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Mediator;

/// <summary>
/// Represents an in-memory store for jobs.
/// </summary>
public class InMemoryStateStoreAsync : IAmAStateStoreAsync
{
    private readonly ConcurrentDictionary<string, Job?> _jobs = new();
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _sinceTime;
    
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<InMemoryStateStoreAsync>();

    /// <summary>
    /// Represents an in-memory store for jobs.
    /// </summary>
    public InMemoryStateStoreAsync(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ??  TimeProvider.System;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="jobAge">A job is due now, less the jobAge span</param>
    /// <param name="cancellationToken">A cancellation token to end the ongoing operation</param>
    /// <returns></returns>
    public Task<IEnumerable<Job>> GetDueJobsAsync(TimeSpan jobAge, CancellationToken cancellationToken = default(CancellationToken))
    {
        var dueJobs = _jobs.Values
            .Where(job =>
            {
                if (job is null || !job.IsScheduled)  return false;
                _sinceTime = _timeProvider.GetUtcNow().Subtract(jobAge);
                return  job.DueTime > _sinceTime;
            })
            .ToList();

        return Task.FromResult((IEnumerable<Job>)dueJobs);
    }

    /// <summary>
    /// Retrieves a job asynchronously by its Id.
    /// </summary>
    /// <param name="id">The Id of the job.</param>
    /// <returns>A task that represents the asynchronous retrieve operation. The task result contains the job if found; otherwise, null.</returns>
    public Task<Job?> GetJobAsync(string? id)
    {
        var tcs = new TaskCompletionSource<Job?>();
        if (id is null)
        {
            tcs.SetResult(null);
            return tcs.Task;
        }

        var job = _jobs.TryGetValue(id, out var state) ? state : null;
        tcs.SetResult(job);
        return tcs.Task;
    }
    
    /// <summary>
    /// Saves the job asynchronously.
    /// </summary>
    /// <typeparam name="TData">The type of the job data.</typeparam>
    /// <param name="job">The job to save.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public Task SaveJobAsync<TData>(Job<TData>? job, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);

        if (job is null) return Task.CompletedTask;

        try
        {
            _jobs[job.Id] = job;
            return Task.FromResult(true);
        }
        catch (Exception e)
        {
            s_logger.LogError($"Error saving job {job.Id} to in-memory store: {e.Message}"); 
            return Task.FromException(e);
        }
    }
}
