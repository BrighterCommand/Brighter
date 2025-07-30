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

namespace Paramore.Brighter.Mediator;

/// <summary>
///  What state is the workflow in
/// </summary>
public enum JobState
{
    Ready,
    Running,
    Waiting,
    Done,
    Faulted
}

/// <summary>
/// empty class, used as marker for the branch data
/// </summary>
public abstract class Job
{
    /// <summary>Used to manage access to state, as the job may be updated from multiple threads</summary>
    protected readonly object LockObject = new();
    
    /// <summary> The time the job is due to run </summary>
    public DateTimeOffset? DueTime { get; set; }

    /// <summary> The id of the workflow, used to save-retrieve it from storage </summary>
    public string Id { get; private set; } = Uuid.NewAsString();
    
    /// <summary> Is the job scheduled to run?</summary>
    public bool IsScheduled => DueTime.HasValue;
    
    /// <summary> The id of the parent job, if this is a child job </summary>
    public string? ParentId { get; set; }
    
    /// <summary> Is the job waiting to be run, running, waiting for a response or finished </summary>
    public JobState State { get; set; }
}

/// <summary>
/// Job represents the current state of the workflow and tracks if it’s awaiting a response.
/// </summary>
/// <typeparam name="TData">The user defined data for the workflow</typeparam>
public class Job<TData> : Job
{
     
    /// <summary> If we are awaiting a response, we store the type of the response and the action to take when it arrives </summary>
    private readonly ConcurrentDictionary<Type, TaskResponse<TData>?> _pendingResponses = new();

    /// <summary>The next step. Steps are a linked list. The final step in the list has null for it's next step. </summary>
    private Step<TData>? _step;

    private ConcurrentDictionary<string, Job> _children = new();  

    /// <summary> The data that is passed between steps of the workflow </summary>
    public TData Data { get; private set; }
    

    /// <summary>
    ///  Constructs a new Job
    /// <param name="data">State which is passed between steps of the workflow</param>
    /// </summary>
    public Job(TData data)
    {
        Data = data;
        State = JobState.Ready;
    }

    /// <summary>
    /// Initializes the steps of the workflow.
    /// </summary>
    /// <param name="firstStep">The first step of the workflow to execute.</param>
    public void InitSteps(Step<TData>? firstStep)
    {
        NextStep(firstStep);
    }

    /// <summary>
    /// Gets the current step of the workflow.
    /// </summary>
    /// <returns>The current step of the workflow.</returns>
    public Step<TData>? CurrentStep()
    {
        return _step;
    }

    /// <summary>
    /// Adds a pending response to the job.
    /// </summary>
    /// <param name="responseType">The expected type of the response</param>
    /// <param name="taskResponse">The task response to add.</param>
    public void AddPendingResponse(Type responseType, TaskResponse<TData>? taskResponse)
    {
        lock (LockObject)
        {
            State = JobState.Waiting;
            if (!_pendingResponses.TryAdd(responseType, taskResponse))
                throw new InvalidOperationException($"A pending response for {responseType} already exists");
        }
    }

    /// <summary>
    /// Finds a pending response by its type.
    /// </summary>
    /// <param name="eventType">The type of the event.</param>
    /// <param name="taskResponse">The task response if found.</param>
    public bool FindPendingResponse(Type eventType, out TaskResponse<TData>? taskResponse)
    {
        return _pendingResponses.TryGetValue(eventType, out taskResponse);
    }

    /// <summary>
    /// Sets the next step of the workflow.
    /// </summary>
    /// <param name="nextStep">The next step to set.</param>
    public void NextStep(Step<TData>? nextStep)
    {
        lock (LockObject)
        {
            _step = nextStep;
            if (_step is not null)
                _step.AddToJob(this);
            else if (State != JobState.Waiting) 
                    State = JobState.Done;
        }
    }
    
    /// <summary>
    /// Removes a pending response from the job, and resets the state to running.
    /// </summary>
    /// <param name="eventType">The type of event that we expect</param>
    /// <returns></returns>
    public bool ResumeAfterEvent(Type eventType)
    {
        if (_step is null) return false;

        lock (LockObject)
        {
            var success = _pendingResponses.Remove(eventType, out _);
            _step.OnCompletion?.Invoke();
            _step = _step.Next;
            if (success) State = JobState.Running;
            return success;
        }
    }

    /// <summary>
    /// Sets an identifier on each child to indicate the parent id
    /// Adds the child to a hashtable of children
    /// </summary>
    /// <param name="child">The job we want to add as a child</param>
    public void AddChildJob(Job<TData> child)
    {
        child.ParentId = Id;
        _children.TryAdd(child.Id, child);
    }

}
