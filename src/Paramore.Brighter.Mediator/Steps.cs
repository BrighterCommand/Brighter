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
public abstract class Step<TData>(string name, Sequential<TData>? next, IStepTask<TData>? stepTask = null, Action? onCompletion = null) 
{
    /// <summary>The name of the step, used for tracing execution</summary>
    public string Name { get; init; } = name;
    
    /// <summary>The next step in the sequence, null if this is the last step</summary>
    protected Sequential<TData>? Next { get; } = next;

    /// <summary>An optional callback to be run, following completion of the step.</summary>
    public Action? OnCompletion { get; } = onCompletion;
    
    /// <summary>The action to be taken with the step.</summary>
    protected IStepTask<TData>? StepTask { get; } = stepTask;
    
    public  virtual Task ExecuteAsync(Job<TData> job, IAmACommandProcessor commandProcessor, CancellationToken cancellationToken)
    {
        StepTask?.HandleAsync(job, commandProcessor, cancellationToken);
        OnCompletion?.Invoke();
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
    Action? onCompletion, 
    Sequential<TData>? next, 
    Action? onFaulted = null, 
    Sequential<TData>? faultNext = null
    ) 
    : Step<TData>(name, next, stepTask, onCompletion)
{
    public override Task ExecuteAsync(Job<TData> state, IAmACommandProcessor commandProcessor, CancellationToken cancellationToken)
    {
        try
        {
            StepTask?.HandleAsync(state, commandProcessor, cancellationToken);
            OnCompletion?.Invoke();
            state.CurrentStep = Next;
        }
        catch (Exception)
        {
            onFaulted?.Invoke();
            state.CurrentStep = faultNext;
        }
        return Task.CompletedTask;
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
    public override Task ExecuteAsync(Job<TData> state, IAmACommandProcessor commandProcessor, CancellationToken cancellationToken)
    {
        state.CurrentStep = predicate.IsSatisfiedBy(state.Data) ? nextTrue : nextFalse;
        return Task.CompletedTask;
    }
}

public class ParallelSplit<TData>(string name, Action<TData>? onBranch, params Step<TData>[] branches) 
    : Step<TData>(name, null)
{
    public Step<TData>[] Branches { get; set; } = branches;
    
    public override Task ExecuteAsync(Job<TData> state, IAmACommandProcessor commandProcessor, CancellationToken cancellationToken)
    {
        // Parallel split doesn't directly execute its jobs. 
        // Execution is handled by the Scheduler, which will handle running each branch concurrently. 
        onBranch?.Invoke(state.Data);
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
public class Wait<TData>(string name, TimeSpan duration, Action? onCompletion,  Sequential<TData>? next) 
    : Step<TData>(name, next, null, onCompletion)
{
    public override async Task ExecuteAsync(Job<TData> state, IAmACommandProcessor commandProcessor, CancellationToken cancellationToken)
    {
        await Task.Delay(duration, cancellationToken);
        OnCompletion?.Invoke();
    }
}


