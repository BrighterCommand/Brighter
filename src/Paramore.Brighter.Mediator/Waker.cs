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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Mediator;

/// <summary>
/// The <see cref="Waker{TData}"/> class is responsible for periodically waking up and triggering due jobs in the scheduler.
/// </summary>
/// <typeparam name="TData">The type of the job data.</typeparam>
public class Waker<TData>
{
    private readonly TimeSpan _jobAge;
    private readonly Scheduler<TData> _scheduler;
    private readonly string _wakerName = Uuid.New().ToString("N");
    
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<Waker<TData>>();

    /// <summary>
    /// Initializes a new instance of the <see cref="Waker{TData}"/> class.
    /// </summary>
    /// <param name="jobAge">The age of the job to determine if it is due.</param>
    /// <param name="scheduler">The scheduler to trigger due jobs.</param>
    public Waker(TimeSpan jobAge, Scheduler<TData> scheduler)
    {
        _jobAge = jobAge;
        _scheduler = scheduler;
    }

    /// <summary>
    /// Runs the <see cref="Waker{TData}"/>> asynchronously.
    /// This will periodically wake up and trigger due jobs in the scheduler.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous run operation.</returns>
    public void RunAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        s_logger.LogInformation("Starting waker {WakerName}", _wakerName);
        
        var task = Task.Factory.StartNew(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Wake(cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                cancellationToken.ThrowIfCancellationRequested();

        }, cancellationToken);
        
        Task.WaitAll([task], cancellationToken);
        
        s_logger.LogInformation("Finished waker {WakerName}", _wakerName);
    }

    private async Task Wake(CancellationToken cancellationToken = default(CancellationToken))
    {
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            await _scheduler.TriggerDueJobsAsync(_jobAge, cancellationToken);
            await Task.Delay(_jobAge, cancellationToken);
        }
    }
}
