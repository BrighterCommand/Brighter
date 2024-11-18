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

namespace Paramore.Brighter.Mediator;

/// <summary>
///  What state is the workflow in
/// </summary>
public enum JobState
{
    Ready,
    Running,
    Waiting,
    Done,
}

/// <summary>
/// empty class, used as marker for the branch data
/// </summary>
public abstract class Job { }

/// <summary>
/// Job represents the current state of the workflow and tracks if it’s awaiting a response.
/// </summary>
/// <typeparam name="TData">The user defined data for the workflow</typeparam>
public class Job<TData> : Job
{
    /// <summary> A map of user defined values. Normally, use Data to pass data between steps </summary>
    public Dictionary<string, object> Bag { get; } = new();
    
    /// <summary> What step are we currently at in the workflow </summary>
    public Step<TData>? Step { get; set; }
    
    /// <summary> The data that is passed between steps of the workflow </summary>
    public TData Data { get; } 
    
    /// <summary> The id of the workflow, used to save-retrieve it from storage </summary>
    public  string Id { get; private set; } = Guid.NewGuid().ToString();

    /// <summary> If we are awaiting a response, we store the type of the response and the action to take when it arrives </summary>
    public Dictionary<Type, TaskResponse<TData>> PendingResponses { get; private set; } = new();
    
    /// <summary> Is the workflow currently awaiting an event response </summary>
    public JobState State { get; set; }

    /// <summary>
    ///  Constructs a new Job 
    /// <param name="firstStep">The first step of the workflow to execute.</param>
    /// <param name="data">State which is passed between steps of the workflow</param>
    /// </summary>
    public Job(TData data) 
    {
        Data = data;
        State = JobState.Ready;    
    }
}


