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
using System.Threading.Tasks;

namespace Paramore.Brighter.Mediator;

/// <summary>
/// The <see cref="Scheduler{TData}"/> class orchestrates a workflow by executing each step in a sequence.
/// It uses a command processor and a workflow store to manage the workflow's state and actions.
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
public class Scheduler<TData>
{
    private readonly IAmAJobChannel<TData> _channel;
    private readonly IAmAStateStoreAsync _stateStore;
    private readonly TimeProvider _timeProvider;


    /// <summary>
    /// Initializes a new instance of the <see cref="Scheduler{TData}"/> class.
    /// </summary>
    /// <param name="channel">The <see cref="IAmAJobChannel{TData}"/> over which jobs flow. The <see cref="Scheduler{TData}"/> is a producer
    ///     and the <see cref="Runner{TData}"/> is the consumer from the  channel</param>
    /// <param name="stateStore">A store for pending jobs</param>
    /// <param name="timeProvider">Provides the time for scheduling, defaults to TimeProvider.System</param>
    public Scheduler(IAmAJobChannel<TData> channel, IAmAStateStoreAsync stateStore, TimeProvider? timeProvider = null)
    {
        _timeProvider =  timeProvider ?? TimeProvider.System;
        _channel = channel;
        _stateStore = stateStore;
    }

    /// <summary>
    /// Call this method from a RequestHandler that listens for an expected event. This will process that event if there is a pending response for the event type.
    /// </summary>
    /// <param name="event">The event to process.</param>
    /// <exception cref="InvalidOperationException">Thrown when the workflow has not been initialized.</exception>
     public async Task ResumeAfterEvent(Event @event)
     {
         if (@event.CorrelationId is null)
             throw new InvalidOperationException("CorrelationId should not be null; needed to retrieve state of workflow");
         
         var w = await _stateStore.GetJobAsync(@event.CorrelationId);
        
         if (w is not Job<TData> job)
             throw new InvalidOperationException("Branch has not been stored");
             
         var eventType = @event.GetType();
             
         if (!job.FindPendingResponse(eventType, out TaskResponse<TData>? taskResponse)) 
             return;

         if (taskResponse is null || taskResponse.Parser is null)
             throw new InvalidOperationException($"Parser for event type {eventType} should not be null");

         if (job.CurrentStep() is null)
             throw new InvalidOperationException($"Current step of workflow #{job.Id} should not be null");
             
         taskResponse.Parser(@event, job);
         job.ResumeAfterEvent(eventType);
         
         await ScheduleAsync(job);
    }

    /// <summary>
    /// Runs the job by executing each step in the sequence.
    /// </summary>
    /// <param name="job">The job that we want a runner to execute</param>
    /// <param name="cancellationToken">A cancellation token to end the ongoing operation</param>
    /// <exception cref="InvalidOperationException">Thrown when the job has not been initialized.</exception>
    public async Task ScheduleAsync(Job<TData> job,CancellationToken cancellationToken = default(CancellationToken))
    {
        await _channel.EnqueueJobAsync(job, cancellationToken);
        job.DueTime = null; // Clear any due time after queuing
        await _stateStore.SaveJobAsync(job, cancellationToken);
    }
    
    /// <summary>
    /// Schedules a list of jobs
    /// </summary>
    /// <param name="jobs">The jobs to schedule</param>
    /// <param name="cancellationToken">A cancellation token to terminate the asynchronous operation</param>
    public async Task ScheduleAsync(IEnumerable<Job<TData>> jobs, CancellationToken cancellationToken = default(CancellationToken))
    {
        foreach (var job in jobs)
        {
            await ScheduleAsync(job, cancellationToken);
        }
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="job">The job that we want a runner to execute</param>
    /// <param name="delay">The delay after which to schedule the job</param>
    /// <param name="cancellationToken">A cancellation token to end the ongoing operation</param>
    /// <exception cref="InvalidOperationException">Thrown when the job has not been initialized.</exception>
    public async Task ScheduleAtAsync(Job<TData> job, TimeSpan delay, CancellationToken cancellationToken = default(CancellationToken))
    {
        job.DueTime = _timeProvider.GetUtcNow().Add(delay);
        await _stateStore.SaveJobAsync(job, cancellationToken);
    }
    
    /// <summary>
    /// Finds any jobs that are due to run and schedules them
    /// </summary>
    /// <param name="jobAge">A job is due now, less the jobAge span</param>
    /// <param name="cancellationToken">A cancellation token to end the ongoing operation</param>
    public async Task TriggerDueJobsAsync(TimeSpan jobAge, CancellationToken cancellationToken = default(CancellationToken))
    {
        var dueJobs = await _stateStore.GetDueJobsAsync(jobAge, cancellationToken);

        foreach (var j in dueJobs)
        {
            var job = j as Job<TData>; 
            
            if (job is null)
                continue;
            
            await ScheduleAsync(job, cancellationToken); 
        }
    }

}
