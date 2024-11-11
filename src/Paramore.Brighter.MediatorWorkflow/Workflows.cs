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

namespace Paramore.Brighter.MediatorWorkflow;

/// <summary>
/// Represents a step in the workflow. Steps form a singly linked list.
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
/// <param name="Name">The name of the step.</param>
/// <param name="Action">The action to be taken with the step.</param>
/// <param name="OnCompletion">The action to be taken upon completion of the step.</param>
/// <param name="Next">The next step in the sequence.</param>
public record Step<TData>(string Name, IWorkflowAction<TData> Action, Action OnCompletion, Step<TData>? Next, Action? OnFaulted = null, Step<TData>? FaultNext = null);

/// <summary>
/// Defines an interface for workflow actions.
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
public interface IWorkflowAction<TData> 
{
    /// <summary>
    /// Handles the workflow action.
    /// </summary>
    /// <param name="state">The current state of the workflow.</param>
    /// <param name="commandProcessor">The command processor used to handle commands.</param>
    void Handle(Workflow<TData> state, IAmACommandProcessor commandProcessor);
}

/// <summary>
/// Represents a workflow based on evaluating a specification to determine which one to send
/// </summary>
/// <param name="predicate">The rule that decides between the command issued by each branch</param>
/// <typeparam name="TData">The workflow data, used to make the choice</typeparam>
public class Choice<TData>(
    Func<TData, Step<TData>> OnTrue, 
    Func<TData, Step<TData>> OnFalse,
    ISpecification<TData> predicate
) 
    : IWorkflowAction<TData> 
{
    public void Handle(Workflow<TData> state, IAmACommandProcessor commandProcessor)
    {
        if (state.CurrentStep is null)
            throw new InvalidOperationException("The workflow has not been initialized.");

        state.CurrentStep = state.CurrentStep with
        {
            Next = (predicate.IsSatisfiedBy(state.Data) ? OnTrue(state.Data) : OnFalse(state.Data))
        };
    }
}

/// <summary>
/// Represents a fire-and-forget action in the workflow.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
/// <param name="requestFactory">The factory method to create the request.</param>
public class FireAndForget<TRequest, TData>(
    Func<TRequest> requestFactory
    ) 
    : IWorkflowAction<TData> 
    where TRequest : class, IRequest 
{
    /// <summary>
    /// Handles the fire-and-forget action.
    /// </summary>
    /// <param name="state">The current state of the workflow.</param>
    /// <param name="commandProcessor">The command processor used to handle commands.</param>
    public void Handle(
        Workflow<TData> state, 
        IAmACommandProcessor commandProcessor
        )
    {
        var command = requestFactory();
        command.CorrelationId = state.Id;
        commandProcessor.Send(command);
    }
}

/// <summary>
/// Represents a request-and-reply action in the workflow.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TReply">The type of the reply.</typeparam>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
/// <param name="requestFactory">The factory method to create the request.</param>
/// <param name="replyFactory">The factory method to handle the reply.</param>
public class RequestAndReaction<TRequest, TReply, TData>(
    Func<TRequest> requestFactory, 
    Action<TReply?> replyFactory
    ) 
    : IWorkflowAction<TData> 
    where TRequest : class, IRequest
    where TReply : Event 
{
    /// <summary>
    /// Handles the request-and-reply action.
    /// </summary>
    /// <param name="state">The current state of the workflow.</param>
    /// <param name="commandProcessor">The command processor used to handle commands.</param>
    public void Handle(Workflow<TData> state, IAmACommandProcessor commandProcessor)
    {
        var command = requestFactory();
        command.CorrelationId = state.Id;
        commandProcessor.Send(command);
       
        state.PendingResponses.Add(typeof(TReply), new WorkflowResponse<TData>((reply, _) => replyFactory(reply as TReply), typeof(TReply), null));
 
    }
}

/// <summary>
/// 
/// </summary>
/// <param name="requestFactory"></param>
/// <param name="replyFactory"></param>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TReply"></typeparam>
/// <typeparam name="TData"></typeparam>
/// <typeparam name="TFault"></typeparam>
public class RobustRequestAndReaction<TRequest, TReply, TFault, TData>(
    Func<TRequest> requestFactory,
    Action<TReply?> replyFactory,
    Action<TFault?> faultFactory
)
    : IWorkflowAction<TData> 
    where TRequest : class, IRequest
    where TReply : Event
    where TFault: Event
{
    public void Handle(Workflow<TData> state, IAmACommandProcessor commandProcessor)
    {
        var command = requestFactory();
        command.CorrelationId = state.Id;
        commandProcessor.Send(command);
        
        state.PendingResponses.Add(typeof(TReply), new WorkflowResponse<TData>((reply, _) => replyFactory(reply as TReply), typeof(TReply), typeof(TFault)));
        state.PendingResponses.Add(typeof(TFault), new WorkflowResponse<TData>((reply, _) => faultFactory(reply as TFault), typeof(TReply), typeof(TFault)));}
}
