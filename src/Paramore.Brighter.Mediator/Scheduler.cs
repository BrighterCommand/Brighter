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
using System.Threading.Tasks;

namespace Paramore.Brighter.Mediator;

/// <summary>
/// The <see cref="Scheduler{TData}"/> class orchestrates a workflow by executing each step in a sequence.
/// It uses a command processor and a workflow store to manage the workflow's state and actions.
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
public class Scheduler<TData> 
{
    private readonly IAmACommandProcessor _commandProcessor;
    private readonly IAmAJobChannel<TData> _channel;
    private readonly IAmAJobStoreAsync _jobStoreAsync;


    /// <summary>
    /// Initializes a new instance of the <see cref="Scheduler{TData}"/> class.
    /// </summary>
    /// <param name="commandProcessor">The command processor used to handle commands.</param>
    /// <param name="channel">The <see cref="IAmAJobChannel{TData}"/> over which jobs flow. The <see cref="Scheduler{TData}"/> is a producer
    /// and the <see cref="Runner{TData}"/> is the consumer from the  channel</param>
    /// <param name="jobStoreAsync">A store for pending jobs</param>
    public Scheduler(IAmACommandProcessor commandProcessor, IAmAJobChannel<TData> channel, IAmAJobStoreAsync jobStoreAsync)
    {
        _commandProcessor = commandProcessor;
        _channel = channel;
        _jobStoreAsync = jobStoreAsync;
    }

    /// <summary>
    /// Runs the job by executing each step in the sequence.
    /// </summary>
    /// <param name="job"></param>
    /// <exception cref="InvalidOperationException">Thrown when the job has not been initialized.</exception>
    public async Task ScheduleAsync(Job<TData> job)
    {
        await _channel.EnqueueJobAsync(job);
    }

    /// <summary>
    /// Call this method from a RequestHandler that listens for an expected event. This will process that event if there is a pending response for the event type.
    /// </summary>
    /// <param name="event">The event to process.</param>
    /// <exception cref="InvalidOperationException">Thrown when the workflow has not been initialized.</exception>
     public async Task ReceiveWorkflowEvent(Event @event)
     {
         if (@event.CorrelationId is null)
             throw new InvalidOperationException("CorrelationId should not be null; needed to retrieve state of workflow");
         
         var w = await _jobStoreAsync.GetJobAsync(@event.CorrelationId);
        
         if (w is not Job<TData> job)
             throw new InvalidOperationException("Branch has not been stored");
             
         var eventType = @event.GetType();
             
         if (!job.PendingResponses.TryGetValue(eventType, out TaskResponse<TData>? taskResponse)) 
             return;

         if (taskResponse.Parser is null)
             throw new InvalidOperationException($"Parser for event type {eventType} should not be null");

         if (job.CurrentStep is null)
             throw new InvalidOperationException($"Current step of workflow #{job.Id} should not be null");
             
         taskResponse.Parser(@event, job);
         job.CurrentStep.OnCompletion?.Invoke();
         job.State = JobState.Running;
         job.PendingResponses.Remove(eventType);
    }
}
