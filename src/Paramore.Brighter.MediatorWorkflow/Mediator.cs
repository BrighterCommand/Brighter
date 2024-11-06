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
/// The `Mediator<TData>` class orchestrates a workflow by executing each step in a sequence.
/// It uses a command processor and a workflow store to manage the workflow's state and actions.
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
public class Mediator<TData> where TData : IAmTheWorkflowData
{
    private readonly IAmACommandProcessor _commandProcessor;
    private readonly IAmAWorkflowStore _workflowStore;


    /// <summary>
    /// Initializes a new instance of the <see cref="Mediator{TData}"/> class.
    /// </summary>
    /// <param name="commandProcessor">The command processor used to handle commands.</param>
    /// <param name="workflowStore">The workflow store used to store and retrieve workflows.</param>
    public Mediator(IAmACommandProcessor commandProcessor, IAmAWorkflowStore workflowStore)
    {
        _commandProcessor = commandProcessor;
        _workflowStore = workflowStore;
    }

    /// <summary>
    /// Runs the workflow by executing each step in the sequence.
    /// </summary>
    /// <param name="firstStep">The first step of the workflow to execute.</param>
    /// <exception cref="InvalidOperationException">Thrown when the workflow has not been initialized.</exception>
    public void RunWorkFlow(Step<TData>? firstStep)
    {                       
        var step = firstStep;
        while (step is not null)
        {
            step.Flow.State = WorkflowState.Running;
            step.Flow.CurrentStep = step;
            step.Action.Handle(step.Flow, _commandProcessor);
            step.OnCompletion();
            step = step.Next;
        }
    }

    /// <summary>
    /// Call this method from a RequestHandler that listens for an expected event. This will process that event if there is a pending response for the event type.
    /// </summary>
    /// <param name="event">The event to process.</param>
    /// <exception cref="InvalidOperationException">Thrown when the workflow has not been initialized.</exception>
     public void ReceiveWorkflowEvent(Event @event)
     {
         if (@event.CorrelationId is null)
             throw new InvalidOperationException("CorrelationId should not be null; needed to retrieve state of workflow");
         
        var w = _workflowStore.GetWorkflow(@event.CorrelationId);
        
        if (w is not Workflow<TData> workflow)
            throw new InvalidOperationException("Workflow has not been stored");
             
        var eventType = @event.GetType();
             
        if (!workflow.PendingResponses.TryGetValue(eventType, out Action<Event, Workflow<TData>>? replyFactory)) 
            return;

        if (workflow.CurrentStep is null)
            throw new InvalidOperationException($"Current step of workflow #{workflow.Id} should not be null");
             
        replyFactory(@event, workflow);
        workflow.CurrentStep.OnCompletion();
        workflow.State = WorkflowState.Running;
        workflow.PendingResponses.Remove(eventType);
    }
    
    /// <summary>
    ///  Waits for a workflow event to be received.
    /// - Stores the workflow state in the workflow store.
    /// - Sets the workflow state to waiting.
    /// - Sets the callback for the workflow to run on completion
    /// </summary>
    public void WaitOnWorkflowEvent()
    {
    }
}
