using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Mediator;

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
    Job<TData> job, 
    IStepTask<TData>? stepTask = null, 
    Action? onCompletion = null) 
{
    /// <summary>The name of the step, used for tracing execution</summary>
    public string Name { get; init; } = name;
    
    /// <summary>The next step in the sequence, null if this is the last step</summary>
    protected Sequential<TData>? Next { get; } = next;

    /// <summary> Which job is being executed by the step. </summary>
    public Job<TData> Job { get; } = job;

    /// <summary>An optional callback to be run, following completion of the step.</summary>
    public Action? OnCompletion { get; } = onCompletion;
    
    /// <summary>The action to be taken with the step.</summary>
    protected IStepTask<TData>? StepTask { get; } = stepTask;

    public abstract Task ExecuteAsync(IAmACommandProcessor commandProcessor, CancellationToken cancellationToken);
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
    Job<TData> job,
    Action? onCompletion,
    Sequential<TData>? nextTrue,
    Sequential<TData>? nextFalse
)
    : Step<TData>(name, null, job, null, onCompletion)
{
    public override Task ExecuteAsync(IAmACommandProcessor commandProcessor, CancellationToken cancellationToken)
    {
        Job.Step = predicate.IsSatisfiedBy(Job.Data) ? nextTrue : nextFalse;
        return Task.CompletedTask;
    }
}

public class ParallelSplit<TData>(
    string name, 
    Job<TData> job,
    Action<TData>? onBranch, 
    params Step<TData>[] branches
    ) 
    : Step<TData>(name, null, job)
{
    public Step<TData>[] Branches { get; set; } = branches;
    
    public override Task ExecuteAsync(IAmACommandProcessor commandProcessor, CancellationToken cancellationToken)
    {
        // Parallel split doesn't directly execute its jobs. 
        // Execution is handled by the Scheduler, which will handle running each branch concurrently. 
        onBranch?.Invoke(Job.Data);
        return Task.CompletedTask;
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
    Job<TData> job,
    Action? onCompletion, 
    Sequential<TData>? next, 
    Action? onFaulted = null, 
    Sequential<TData>? faultNext = null
) 
    : Step<TData>(name, next, job, stepTask, onCompletion)
{
    public override Task ExecuteAsync(IAmACommandProcessor commandProcessor, CancellationToken cancellationToken)
    {
        try
        {
            StepTask?.HandleAsync(Job, commandProcessor, cancellationToken);
            OnCompletion?.Invoke();
            Job.Step = Next;
        }
        catch (Exception)
        {
            onFaulted?.Invoke();
            Job.Step = faultNext;
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Allows the workflow to pause. This is a blocking operation that pauses the executing thread
/// </summary>
/// <param name="name">The name of the step, used for tracing execution</param>
/// <param name="duration">The period for which we pause</param>
/// <param name="onCompletion">An optional callback to run, following completion of the step</param>
/// <param name="next">The next step in the sequence, null if this is the last step.</param>
/// <typeparam name="TData">The data that the step operates over</typeparam>
public class Wait<TData>(
    string name, 
    TimeSpan duration, 
    Job<TData> job,
    Action? onCompletion,  
    Sequential<TData>? next
    ) 
    : Step<TData>(name, next, job, null, onCompletion)
{
    public override async Task ExecuteAsync(IAmACommandProcessor commandProcessor, CancellationToken cancellationToken)
    {
        await Task.Delay(duration, cancellationToken);
        OnCompletion?.Invoke();
    }
}


