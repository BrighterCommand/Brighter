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
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Mediator;

public enum StepState
{
    Queued,
    Running, 
    Done,
    Faulted
}

/// <summary>
/// The base type for a step in the workflow.
/// </summary>
/// <param name="name">The name of the step, used for tracing execution</param>
/// <param name="next">The next step in the sequence, null if this is the last step.</param>
/// <param name="stepTask">The action to be taken with the step, null if no action</param>
/// <param name="onCompletion">An optional callback to run, following completion of the step</param>
/// <typeparam name="TData">The data that the step operates over</typeparam>
public abstract class Step<TData>(
    string name,
    Sequential<TData>? next,
    IStepTask<TData>? stepTask = null,
    Action? onCompletion = null) 
{
    /// <summary> Which job is being executed by the step. </summary>
    protected Job<TData>? Job ;

    /// <summary> The logger for the step. </summary>
    protected static readonly ILogger s_logger = ApplicationLogging.CreateLogger<Step<TData>>();
    
    /// <summary>The name of the step, used for tracing execution</summary>
    public string Name { get; init; } = name;
    
    /// <summary>The next step in the sequence, null if this is the last step</summary>
    protected internal Step<TData>? Next { get; } = next;

    /// <summary>An optional callback to be run, following completion of the step.</summary>
    protected internal Action? OnCompletion { get; } = onCompletion;
    
    /// <summary>The action to be taken with the step.</summary>
    protected readonly IStepTask<TData>? StepTask = stepTask;
    
    public StepState? State { get; set; }

    /// <summary>
    ///  The work of the step is done here. Note that this is an abstract method, so it must be implemented by the derived class.
    ///   Your application logic does not live in the step. Instead, you raise a command to a handler, which will do the work.
    ///  The purpose of the step is to orchestrate the workflow, not to do the work.
    /// </summary>
    /// <param name="stateStore">If the step updates the job, it needs to save its new state</param>
    /// <param name="commandProcessor">The command processor, used to send requests to complete steps</param>
    /// <param name="scheduler">The scheduler, used for queuing jobs that need to wait</param>
    /// <param name="cancellationToken">The cancellation token, to end this workflow</param>
    /// <returns></returns>
    public abstract Task ExecuteAsync(
        IAmAStateStoreAsync stateStore, 
        IAmACommandProcessor? commandProcessor = null, 
        Scheduler<TData>? scheduler = null,
        CancellationToken cancellationToken = default(CancellationToken)
        );
    
    /// <summary>
    /// Sets the job that is executing us
    /// </summary>
    /// <param name="job">The job that we are executing under</param>
    public void AddToJob(Job<TData> job)
    {
        Job = job;
        State = StepState.Queued;
    }
}

/// <summary>
/// Allows the workflow to branch on a choice, taking either a right or left path. 
/// </summary>
/// <param name="name">The name of the step, used for tracing execution</param>
/// <param name="predicate">A composite specification that can be evaluated to determine the path to choose</param>
/// <param name="onCompletion">An optional callback to run, following completion of the step</param>
/// <param name="nextTrue">The next step in the sequence, if the predicate evaluates to true, null if this is the last step.</param>
/// <param name="nextFalse">The next step in the sequence, if the predicate evaluates to false, null if this is the last step.</param>
/// <typeparam name="TData">The data that the step operates over</typeparam>
public class ExclusiveChoice<TData>(
    string name,
    ISpecification<TData> predicate,
    Action? onCompletion,
    Sequential<TData>? nextTrue,
    Sequential<TData>? nextFalse
)
    : Step<TData>(name, null, null, onCompletion)
{
    /// <summary>
    ///  The work of the step is done here. Note that this is an abstract method, so it must be implemented by the derived class.
    ///   Your application logic does not live in the step. Instead, you raise a command to a handler, which will do the work.
    ///  The purpose of the step is to orchestrate the workflow, not to do the work.
    /// </summary>
    /// <param name="stateStore">If the step updates the job, it needs to save its new state</param>
    /// <param name="commandProcessor">The command processor, used to send requests to complete steps</param>
    /// <param name="scheduler">The scheduler, used for queuing jobs that need to wait</param>
    /// <param name="cancellationToken">The cancellation token, to end this workflow</param>
    /// <returns></returns>
    public override async Task ExecuteAsync(
        IAmAStateStoreAsync stateStore, 
        IAmACommandProcessor? commandProcessor = null, 
        Scheduler<TData>? scheduler = null,
        CancellationToken cancellationToken = default(CancellationToken)
    )   
    {
        if (Job is null)
            throw new InvalidOperationException("Job is null");
        
        State = StepState.Running;
        
        var step = predicate.IsSatisfiedBy(Job.Data) ? nextTrue : nextFalse;

        State = StepState.Done;
        
        if (step != null)
            step.State = StepState.Queued;
        
        Job.NextStep(step);
        OnCompletion?.Invoke();
        await stateStore.SaveJobAsync(Job, cancellationToken);
        
    }
}

public class ParallelSplit<TData>(
    string name,
    Func<TData, IEnumerable<Step<TData>>>? onMap)
    : Step<TData>(name, null)
{
    /// <summary>
    ///  The work of the step is done here. Note that this is an abstract method, so it must be implemented by the derived class.
    ///   Your application logic does not live in the step. Instead, you raise a command to a handler, which will do the work.
    ///  The purpose of the step is to orchestrate the workflow, not to do the work.
    /// </summary>
    /// <param name="stateStore">If the step updates the job, it needs to save its new state</param>
    /// <param name="commandProcessor">The command processor, used to send requests to complete steps</param>
    /// <param name="scheduler">The scheduler, used for queuing jobs that need to wait</param>
    /// <param name="cancellationToken">The cancellation token, to end this workflow</param>
    /// <returns></returns>
    public override async Task ExecuteAsync(
        IAmAStateStoreAsync stateStore, 
        IAmACommandProcessor? commandProcessor = null, 
        Scheduler<TData>? scheduler = null,
        CancellationToken cancellationToken = default(CancellationToken)
    )   
    {
        if (Job is null)
            throw new InvalidOperationException("Job is null");
        
        if (onMap is null)
           throw new InvalidOperationException("onMap is null; a ParallelSplit Step must have a mapping function to map to multiple branches");
           
        if (scheduler is null)
            throw new InvalidOperationException("Scheduler is null; a ParallelSplit Step must have a scheduler to schedule the next step");
        
        State = StepState.Running;
        
        //Map to multiple branches
        var branches = onMap?.Invoke(Job.Data);
        
        if (branches is null)
            return;
        
        foreach (Step<TData> branch in branches)
        {
            var childJob = new Job<TData>(Job.Data);
            childJob.AddChildJob(Job);
            childJob.InitSteps(branch);
            await scheduler.ScheduleAsync(childJob, cancellationToken);    
        }
        
        State = StepState.Done;
        
        //NOTE: parallel split is a final step - this might change when we bring in merge
        Job.NextStep(null);
        await stateStore.SaveJobAsync(Job, cancellationToken);
    }
}

/// <summary>
/// Represents a sequential step in the workflow. Control flows to the next step in the list, or ends if next is null.
/// A set of sequential steps for a linked list.
/// </summary>
/// <param name="name">The name of the step, used for tracing execution</param>
/// <param name="stepTask">The action to be taken with the step.</param>
/// <param name="onCompletion">An optional callback to run, following completion of the step</param>
/// <param name="next">The next step in the sequence, null if this is the last step.</param>
/// <param name="onFaulted">An optional callback to run, following a faulted execution of the step</param>
/// <param name="faultNext">The next step in the sequence, following a faulted execution of the step</param>
/// <typeparam name="TData">The data that the step operates over</typeparam>
public class Sequential<TData>(
    string name, 
    IStepTask<TData> stepTask, 
    Action? onCompletion, 
    Sequential<TData>? next, 
    Action? onFaulted = null, 
    Sequential<TData>? faultNext = null
) 
    : Step<TData>(name, next, stepTask, onCompletion)
{
    /// <summary>
    ///  The work of the step is done here. Note that this is an abstract method, so it must be implemented by the derived class.
    ///   Your application logic does not live in the step. Instead, you raise a command to a handler, which will do the work.
    ///  The purpose of the step is to orchestrate the workflow, not to do the work.
    /// </summary>
    /// <param name="stateStore">If the step updates the job, it needs to save its new state</param>
    /// <param name="commandProcessor">The command processor, used to send requests to complete steps</param>
    /// <param name="scheduler">The scheduler, used for queuing jobs that need to wait</param>
    /// <param name="cancellationToken">The cancellation token, to end this workflow</param>
    /// <returns></returns>
    public override async Task ExecuteAsync(
        IAmAStateStoreAsync stateStore, 
        IAmACommandProcessor? commandProcessor = null, 
        Scheduler<TData>? scheduler = null,
        CancellationToken cancellationToken = default(CancellationToken)
    )    
    {
        if (Job is null)
            throw new InvalidOperationException("Job is null");
        
        if (StepTask is null)
        {
            s_logger.LogWarning("No task to execute for {Name}", Name);
            State = StepState.Done;
            await stateStore.SaveJobAsync(Job, cancellationToken);
            return;
        }
        
        State = StepState.Running;

        try
        {
            await StepTask.HandleAsync(Job, commandProcessor, stateStore, cancellationToken);
            OnCompletion?.Invoke();
            State = StepState.Done;
            
            if(Next != null)
                Next.State = StepState.Queued;
            
            Job.NextStep(Next);
            await stateStore.SaveJobAsync(Job, cancellationToken);
        }
        catch (Exception)
        {
            Job.State = JobState.Faulted;
            onFaulted?.Invoke();
            
            if (faultNext != null)
                faultNext.State = StepState.Queued;
            
            Job.NextStep(faultNext); 
            State = StepState.Faulted;
            await stateStore.SaveJobAsync(Job, cancellationToken);
        }
    }
}

/// <summary>
/// Allows the workflow to pause. This is a blocking operation that pauses the executing thread
/// </summary>
/// <typeparam name="TData">The data that the step operates over</typeparam>
public class Wait<TData> : Step<TData>
{
    private readonly TimeSpan _duration;

    /// <summary>
    /// Allows the workflow to pause. This is a blocking operation that pauses the executing thread
    /// </summary>
    /// <param name="name">The name of the step, used for tracing execution</param>
    /// <param name="duration">The period for which we pause</param>
    /// <param name="next">The next step in the sequence, null if this is the last step.</param>
    /// <typeparam name="TData">The data that the step operates over</typeparam>
    public Wait(string name, TimeSpan duration, Sequential<TData>? next) 
        : base(name, next)
    {
        _duration = duration;
    }

    /// <summary>
    ///  The work of the step is done here. Note that this is an abstract method, so it must be implemented by the derived class.
    ///   Your application logic does not live in the step. Instead, you raise a command to a handler, which will do the work.
    ///  The purpose of the step is to orchestrate the workflow, not to do the work.
    /// </summary>
    /// <param name="stateStore">If the step updates the job, it needs to save its new state</param>
    /// <param name="commandProcessor">The command processor, used to send requests to complete steps</param>
    /// <param name="scheduler">The scheduler, used for queuing jobs that need to wait</param>
    /// <param name="cancellationToken">The cancellation token, to end this workflow</param>
    /// <returns></returns>
    public override async Task ExecuteAsync(
        IAmAStateStoreAsync stateStore, 
        IAmACommandProcessor? commandProcessor = null, 
        Scheduler<TData>? scheduler = null,
        CancellationToken cancellationToken = default(CancellationToken)
    )   
    {
        if (Job is null)
            throw new InvalidOperationException("Job is null");
        
        if (scheduler is null)
            throw new InvalidOperationException("Scheduler is null; a Wait Step must have a scheduler to schedule the next step");

        if (Next == null)
            throw new InvalidOperationException("Next step is empty; wait schedule the next step, so it cannot be empty");
        
        State = StepState.Running;
        
        Job.DueTime = DateTime.UtcNow.Add(_duration);
        
        State = StepState.Done;
        
        Next.State = StepState.Queued;
        
        Job.NextStep(Next);
        
        Job.State = JobState.Waiting;
        
        //this call will save the state of the Job, so no need to do it twice
        await scheduler.ScheduleAtAsync(Job, _duration, cancellationToken);
    }
}


