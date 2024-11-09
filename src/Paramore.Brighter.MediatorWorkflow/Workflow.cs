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
///  What state is the workflow in
/// </summary>
public enum WorkflowState
{
    Ready,
    Running,
    Waiting,
    Done,
}

/// <summary>
/// empty class, used as maker for the workflow data
/// </summary>
public abstract class Workflow { }

/// <summary>
/// Interface for the data that is passed between steps in the workflow
/// </summary>
public interface  IAmTheWorkflowData
{
    /// <summary>
    /// Bucket for  data that is passed between steps in the workflow
    /// </summary>
    public Dictionary<string, object> Bag { get; set; }    
}

/// <summary>
/// Workflow represents the current state of the workflow and tracks if it’s awaiting a response.
/// </summary>
public class Workflow<TData> : Workflow where TData :  IAmTheWorkflowData
{
    /// <summary>
    /// What step are we currently at in the workflow
    /// </summary>
    public Step<TData>? CurrentStep { get; set; }
    
    public TData Data { get; set; } 
    
    /// <summary>
    /// The id of the workflow, used to save-retrieve it from storage
    /// </summary>
    public  string Id { get; private set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// If we are awaiting a response, we store the type of the response and the action to take when it arrives
    /// </summary>
    public Dictionary<Type, Action<Event, Workflow<TData>>> PendingResponses { get; private set; } = new();
    
    /// <summary>
    /// Is the workflow currently awaiting an event response
    /// </summary>
    public WorkflowState State { get; set; } = WorkflowState.Ready;

    /// <summary>
    ///  Constructs a new Workflow 
    /// <param name="firstStep">The first step of the workflow to execute.</param>
    /// <param name="data">State which is passed between steps of the workflow</param>
    /// </summary>
    public Workflow(Step<TData> firstStep, TData data) 
    {
        CurrentStep = firstStep;
        Data = data;
        State = WorkflowState.Ready;    
    }
}

