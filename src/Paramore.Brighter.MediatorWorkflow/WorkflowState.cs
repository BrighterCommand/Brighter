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
/// WorkflowState represents the current state of the workflow and tracks if it’s awaiting a response.
/// </summary>
public class WorkflowState
{
    /// <summary>
    /// Is the workflow currently awaiting an event response
    /// </summary>
    public bool AwaitingResponse { get; set; } = false;
    
    /// <summary>
    /// Used to store data that is passed between steps in the workflow
    /// </summary>
    public Dictionary<string, object> Bag { get; set; } = new();
    
    /// <summary>
    /// What is the current state of the workflow
    /// </summary>
    public Step CurrentStep { get; set; }
    
    /// <summary>
    /// The id of the workflow, used to save-retrieve it from storage
    /// </summary>
    public  Guid Id { get; private set; } = Guid.NewGuid();

    public Dictionary<Type, Action<Event, WorkflowState>> PendingResponses { get; private set; } = new();

    /// <summary>
    ///  Constructs a new WorkflowState 
    /// </summary>
    public WorkflowState() { }
}

