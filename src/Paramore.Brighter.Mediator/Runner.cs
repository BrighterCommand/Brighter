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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Mediator;

/// <summary>
/// The <see cref="Runner{TData}"/> class processes jobs from a job channel and executes them.
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
public class Runner<TData>
{
    private readonly IAmAJobChannel<TData> _channel;
    private readonly IAmAStateStoreAsync _stateStore;
    private readonly IAmACommandProcessor _commandProcessor;
    private readonly Scheduler<TData> _scheduler;

    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<Runner<TData>>();

    /// <summary>
    /// Initializes a new instance of the <see cref="Runner{TData}"/> class.
    /// </summary>
    /// <param name="channel">The job channel to process jobs from.</param>
    /// <param name="stateStore">The job store to save job states.</param>
    /// <param name="commandProcessor">The command processor to handle commands.</param>
    /// <param name="scheduler">The scheduler which allows us to queue work that should be deferred</param>
    public Runner(IAmAJobChannel<TData> channel, IAmAStateStoreAsync stateStore, IAmACommandProcessor commandProcessor, Scheduler<TData> scheduler)
    {
        _channel = channel;
        _stateStore = stateStore;
        _commandProcessor = commandProcessor;
        _scheduler = scheduler;
    }

    /// <summary>
    /// Runs the job processing loop.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await Task.Factory.StartNew(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            await ProcessJobs(cancellationToken);
            
            if (cancellationToken.IsCancellationRequested)
                cancellationToken.ThrowIfCancellationRequested();
            
        }, cancellationToken);
    }

    private async Task Execute(Job<TData>? job, CancellationToken cancellationToken = default)
    {
        if (job is null)
            return;
        
        job.State = JobState.Running;
        await _stateStore.SaveJobAsync(job, cancellationToken);

        var step = job.CurrentStep();
        while (step is not null)
        {
            if (step.State == StepState.Queued)
            {
                await step.ExecuteAsync(_stateStore, _commandProcessor, _scheduler, cancellationToken);
            }

            //if the job  has a pending step, finish execution of this job.
            if (job.State == JobState.Waiting)
                break;
            
            step = job.CurrentStep();
        }
        
        if (job.State != JobState.Waiting) 
            job.State = JobState.Done;
        
    }

    private async Task ProcessJobs(CancellationToken cancellationToken)
    {
        while (!_channel.IsClosed())
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var job = await _channel.DequeueJobAsync(cancellationToken);
            if (job is null)
                continue;
            
            await Execute(job, cancellationToken);
        }
    }
}
