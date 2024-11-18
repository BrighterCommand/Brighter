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
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Mediator;

/// <summary>
/// Defines an interface for workflow actions.
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
public interface IStepTask<TData> 
{
    /// <summary>
    /// Handles the workflow action.
    /// </summary>
    /// <param name="job">The current job of the workflow.</param>
    /// <param name="commandProcessor">The command processor used to handle commands.</param>
    /// <param name="cancellationToken">The cancellation token for this task</param>
    Task HandleAsync(Job<TData>? job, IAmACommandProcessor commandProcessor, CancellationToken cancellationToken);
}

/// <summary>
/// When we are awaiting a response for a workflow, we need to store information about how to continue the workflow
/// after receiving the event. 
/// </summary>
/// <param name="parser">The parser to populate our workflow from the event that forms the response</param>
/// <param name="responseType">The type we expect a response to be - used to check the flow</param>
/// <param name="errorType">The type we expect a fault to be - used to check the flow</param>
/// <typeparam name="TData">The user-defined data, associated with a workflow</typeparam>
public class TaskResponse<TData>(Action<Event, Job<TData>> parser, Type responseType, Type? errorType)
{
    /// <summary>Parses a response to a workflow sequence step</summary>
    public Action<Event, Job<TData>>? Parser { get; set; } = parser;
    
    /// <summary>The type we expect a response to be - used to check the flow</summary>
    public Type? ResponseType  { get; set; } = responseType;
    
    /// <summary>The type we expect a fault to be - used to check the flow</summary>
    public Type? ErrorType { get; set; } = errorType;

    /// <summary>
    /// Do we have an error
    /// </summary>
    /// <returns>True if we have an error, false otherwise</returns>
    public bool HasError() =>  ErrorType is not null; 
}

/// <summary>
/// Essentially a pass through step, it alters <see cref="Job{TData}"/> Data property by running the transform
/// given by onChange over it
/// </summary>
/// <param name="onChange">Takes the <see cref="Job{TData}"/> Data property and transforms it</param>
/// <typeparam name="TData">The workflow data, that we wish to transform</typeparam>
public class ChangeAsync<TData>(
    Func<TData, Task> onChange
) : IStepTask<TData>
{
    
    /// <summary>
    /// Handles the workflow action.
    /// </summary>
    /// <param name="job">The current job of the workflow.</param>
    /// <param name="commandProcessor">The command processor used to handle commands.</param>
    /// <param name="cancellationToken">The cancellation token for this task</param>
    public async Task HandleAsync(Job<TData>? job, IAmACommandProcessor commandProcessor, CancellationToken cancellationToken)
    {
        if (job is null)
            return;
        
        if (cancellationToken.IsCancellationRequested)
            return;
        
        await onChange(job.Data);
    }
}

/// <summary>
/// Represents a fire-and-forget action in the workflow.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
/// <param name="requestFactory">The factory method to create the request.</param>
public class FireAndForgetAsync<TRequest, TData>(
    Func<TRequest> requestFactory
    ) 
    : IStepTask<TData> 
    where TRequest : class, IRequest 
{
    /// <summary>
    /// Handles the fire-and-forget action.
    /// </summary>
    /// <param name="job">The current job of the workflow.</param>
    /// <param name="commandProcessor">The command processor used to handle commands.</param>
    /// <param name="cancellationToken">The cancellation token for this task</param>
    public async Task HandleAsync(Job<TData>? job,  IAmACommandProcessor commandProcessor, CancellationToken cancellationToken)
    {
        if (job is null)
            return;
        
        var command = requestFactory();
        command.CorrelationId = job.Id;
        await commandProcessor.SendAsync(command, cancellationToken: cancellationToken);
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
public class RequestAndReactionAsync<TRequest, TReply, TData>(
    Func<TRequest> requestFactory, 
    Action<TReply?> replyFactory
    ) 
    : IStepTask<TData> 
    where TRequest : class, IRequest
    where TReply : Event 
{
    /// <summary>
    /// Handles the request-and-reply action.
    /// </summary>
    /// <param name="job">The current job of the workflow.</param>
    /// <param name="commandProcessor">The command processor used to handle commands.</param>
    /// <param name="cancellationToken">The cancellation token for this task</param>
    public async Task HandleAsync(Job<TData>? job, IAmACommandProcessor commandProcessor,  CancellationToken cancellationToken)
    {
        if (job is null)
            return;
        
        var command = requestFactory();
        command.CorrelationId = job.Id;
        await commandProcessor.SendAsync(command, cancellationToken: cancellationToken);
       
        job.PendingResponses.Add(typeof(TReply), new TaskResponse<TData>((reply, _) => replyFactory(reply as TReply), typeof(TReply), null));
 
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
public class RobustRequestAndReactionAsync<TRequest, TReply, TFault, TData>(
    Func<TRequest> requestFactory,
    Action<TReply?> replyFactory,
    Action<TFault?> faultFactory
)
    : IStepTask<TData> 
    where TRequest : class, IRequest
    where TReply : Event
    where TFault: Event
{
    /// <summary>
    /// Handles the fire-and-forget action.
    /// </summary>
    /// <param name="job">The current job of the workflow.</param>
    /// <param name="commandProcessor">The command processor used to handle commands.</param>
    /// <param name="cancellationToken">The cancellation token for this task</param>
    public async Task HandleAsync(Job<TData>? job, IAmACommandProcessor commandProcessor, CancellationToken cancellationToken)
    {
        if (job is null)
            return;
        
        var command = requestFactory();
        command.CorrelationId = job.Id;
        await commandProcessor.SendAsync(command, cancellationToken: cancellationToken);
        
        job.PendingResponses.Add(typeof(TReply), new TaskResponse<TData>((reply, _) => replyFactory(reply as TReply), typeof(TReply), typeof(TFault)));
        job.PendingResponses.Add(typeof(TFault), new TaskResponse<TData>((reply, _) => faultFactory(reply as TFault), typeof(TReply), typeof(TFault)));}
}


