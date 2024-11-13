using System;
using System.Threading.Tasks;

namespace Paramore.Brighter.MediatorWorkflow;

/// <summary>
/// The base type for a step in the workflow.
/// </summary>
/// <param name="name">The name of the step, used for tracing execution</param>
/// <param name="next">The next step in the sequence, null if this is the last step.</param>
/// <param name="stepTask">The action to be taken with the step, null if no action</param>
/// <param name="onCompletion">An optional callback to run, following completion of the step</param>
/// <typeparam name="TData">The data that the step operates over</typeparam>
public abstract class Step<TData>(string name, Sequence<TData>? next, IStepTask<TData>? stepTask = null, Action? onCompletion = null) 
{
    /// <summary>The name of the step, used for tracing execution</summary>
    public string Name { get; init; } = name;
    
    /// <summary>The next step in the sequence, null if this is the last step</summary>
    protected Sequence<TData>? Next { get; } = next;

    /// <summary>An optional callback to be run, following completion of the step.</summary>
    public Action? OnCompletion { get; } = onCompletion;
    
    /// <summary>The action to be taken with the step.</summary>
    protected IStepTask<TData>? StepTask { get; } = stepTask;
    
    public virtual void Execute(Workflow<TData> state, IAmACommandProcessor commandProcessor)
    {
        StepTask?.Handle(state, commandProcessor);
        OnCompletion?.Invoke();
    }
}

/// <summary>
/// Represents a step in the workflow. Steps form a singly linked list.
/// </summary>
/// <param name="name">The name of the step, used for tracing execution</param>
/// <param name="stepTask">The action to be taken with the step.</param>
/// <param name="onCompletion">An optional callback to run, following completion of the step</param>
/// <param name="next">The next step in the sequence, null if this is the last step.</param>
/// <param name="onFaulted">An optional callback to run, following a faulted execution of the step</param>
/// <param name="faultNext">The next step in the sequence, following a faulted execution of the step</param>
/// <typeparam name="TData">The data that the step operates over</typeparam>
public class Sequence<TData>(
    string name, 
    IStepTask<TData> stepTask, 
    Action? onCompletion, 
    Sequence<TData>? next, 
    Action? onFaulted = null, 
    Sequence<TData>? faultNext = null
    ) 
    : Step<TData>(name, next, stepTask, onCompletion)
{
    public override void Execute(Workflow<TData> state, IAmACommandProcessor commandProcessor)
    {
        try
        {
            StepTask?.Handle(state, commandProcessor);
            OnCompletion?.Invoke();
            state.CurrentStep = Next;
        }
        catch (Exception)
        {
            onFaulted?.Invoke();
            state.CurrentStep = faultNext;
        }
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
    Sequence<TData>? nextTrue,
    Sequence<TData>? nextFalse
)
    : Step<TData>(name, null, null, onCompletion)
{
    public override void Execute(Workflow<TData> state, IAmACommandProcessor commandProcessor)
    {
        state.CurrentStep = predicate.IsSatisfiedBy(state.Data) ? nextTrue : nextFalse;
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
public class Wait<TData>(string name, TimeSpan duration, Action? onCompletion,  Sequence<TData>? next) 
    : Step<TData>(name, next, null, onCompletion)
{
    public void Handle(Workflow<TData> state, IAmACommandProcessor commandProcessor)
    {
        Task.Delay(duration).Wait();
        OnCompletion?.Invoke();
    }
}


