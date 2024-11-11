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
/// The <see cref="Mediator{TData}"/> class orchestrates a workflow by executing each step in a sequence.
/// It uses a command processor and a workflow store to manage the workflow's state and actions.
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
public class Mediator<TData> 
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
    /// <param name="workflow"></param>
    /// <exception cref="InvalidOperationException">Thrown when the workflow has not been initialized.</exception>
    public void RunWorkFlow(Workflow<TData> workflow)
    {
        if (workflow.CurrentStep is null)
        {
            workflow.State = WorkflowState.Done;
            return;
        }
        
        workflow.State = WorkflowState.Running;
        _workflowStore.SaveWorkflow(workflow);
        
        while (workflow.CurrentStep is not null)
        {
           workflow.CurrentStep.Action.Handle(workflow, _commandProcessor);
           workflow.CurrentStep.OnCompletion();
           workflow.CurrentStep = workflow.CurrentStep.Next;
           _workflowStore.SaveWorkflow(workflow);
        }
        
        workflow.State = WorkflowState.Done;
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
             
        if (!workflow.PendingResponses.TryGetValue(eventType, out WorkflowResponse<TData>? workflowResponse)) 
            return;

        if (workflowResponse.Parser is null)
            throw new InvalidOperationException($"Parser for event type {eventType} should not be null");

        if (workflow.CurrentStep is null)
            throw new InvalidOperationException($"Current step of workflow #{workflow.Id} should not be null");
             
        workflowResponse.Parser(@event, workflow);
        workflow.CurrentStep.OnCompletion();
        workflow.State = WorkflowState.Running;
        workflow.PendingResponses.Remove(eventType);
    }
}
