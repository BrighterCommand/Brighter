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

namespace Paramore.Brighter.MediatorWorkflow;

/// <summary>
/// The mediator orchestrates a workflow, executing each step in the sequence. 
/// </summary>
/// <param name="steps"></param>
/// <param name="commandProcessor"></param>
/// <param name="stateStore"></param>
public class Mediator(IAmACommandProcessor commandProcessor)
{
    /// <summary>
    /// 
    /// Runs the workflow by executing each step in the sequence.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the workflow has not been initialized.</exception>
    public void RunWorkFlow(Step? firstStep)
    {
        var step = firstStep;
        while (step is not null)
        {
            step.Action.Handle(step.Flow, commandProcessor);
            step.OnCompletion();
            step = step.Next;
        }
    }

    /// <summary>
    /// Receives an event and processes it if there is a pending response for the event type.
    /// </summary>
    /// <param name="event">The event to process.</param>
    /// <exception cref="InvalidOperationException">Thrown when the workflow has not been initialized.</exception>
     public void ReceiveWorklowEvent(Event @event)
     {
        //     var state = stateStore.GetState(@event.CorrelationId);
        //     
        //     var eventType = @event.GetType();
        //     
        //     if (!state.PendingResponses.TryGetValue(eventType, out Action<Event, Workflow> replyFactory)) 
        //         return;
        //     
        //     replyFactory(@event, state);
        //     state.PendingResponses.Remove(eventType);
    }
}
