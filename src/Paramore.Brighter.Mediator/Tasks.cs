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
    /// <param name="stateStore">Used to store the state of a job, if it is altered in the handler</param>
    /// <param name="cancellationToken">The cancellation token for this task</param>
    Task HandleAsync(Job<TData>? job, IAmACommandProcessor? commandProcessor, IAmAStateStoreAsync stateStore, CancellationToken cancellationToken = default(CancellationToken));
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
    /// <param name="stateStore">Used to store the state of a job, if it is altered in the handler</param>
    /// <param name="cancellationToken">The cancellation token for this task</param>
    public async Task HandleAsync(
        Job<TData>? job, 
        IAmACommandProcessor? commandProcessor, 
        IAmAStateStoreAsync stateStore, 
        CancellationToken cancellationToken = default(CancellationToken)
        )
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
    Func<TData, TRequest> requestFactory
    ) 
    : IStepTask<TData> 
    where TRequest : class, IRequest 
{
    /// <summary>
    /// Handles the fire-and-forget action.
    /// </summary>
    /// <param name="job">The current job of the workflow.</param>
    /// <param name="commandProcessor">The command processor used to handle commands.</param>
    /// <param name="stateStore">Used to store the state of a job, if it is altered in the handler</param>
    /// <param name="cancellationToken">The cancellation token for this task</param>
    public async Task HandleAsync(
        Job<TData>? job,  
        IAmACommandProcessor? commandProcessor, 
        IAmAStateStoreAsync stateStore, 
        CancellationToken cancellationToken = default(CancellationToken)
        )
    {
        if (job is null)
            return;
        
        if (commandProcessor is null)
            throw new ArgumentNullException(nameof(commandProcessor));
        
        var command = requestFactory(job.Data);
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
    Func<TData, TRequest> requestFactory, 
    Action<TReply?, TData> replyFactory
    ) 
    : IStepTask<TData> 
    where TRequest : class, IRequest
    where TReply : Event 
{
    /// <summary>
    /// Handles the request-and-reply action.
    /// </summary>
    /// <remarks>The logic here has to add the pending response, before the call to send the request. This is because the call to publish is not
    /// over a bus, so it occurs sequentially within the Send before it exits. The event handler calls the <see cref="Scheduler{TData}"/>'s
    /// ResumeAfterEvent method to schedule handling the response. This will look up the pending response. So it needs to be stored prior
    /// to this call completing</remarks>
    /// <param name="job">The current job of the workflow.</param>
    /// <param name="stateStore">The state store, required so that we can save the job state before sending the message</param>
    /// <param name="commandProcessor">The command processor used to handle commands.</param>
    /// <param name="cancellationToken">The cancellation token for this task</param>
    public async Task HandleAsync(
        Job<TData>? job, 
        IAmACommandProcessor? commandProcessor, 
        IAmAStateStoreAsync stateStore, 
        CancellationToken cancellationToken = default(CancellationToken)
        )
    {
        if (job is null)
            return;
        
        if (commandProcessor is null)
            throw new ArgumentNullException(nameof(commandProcessor));
        
        var command = requestFactory(job.Data);
        command.CorrelationId = job.Id;
        
        job.AddPendingResponse(
            typeof(TReply), 
            new TaskResponse<TData>((reply, _) => replyFactory(reply as TReply, job.Data), typeof(TReply), 
                null
                )
            );
        await stateStore.SaveJobAsync(job, cancellationToken);
        
        await commandProcessor.SendAsync(command, cancellationToken: cancellationToken);
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
    Func<TData, TRequest> requestFactory,
    Action<TReply?, TData> replyFactory,
    Action<TFault?, TData> faultFactory
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
    /// <param name="stateStore">The state store, required so that we can save the job state before sending the message</param>
    /// <param name="cancellationToken">The cancellation token for this task</param>
    public async Task HandleAsync(
        Job<TData>? job, 
        IAmACommandProcessor? commandProcessor,
        IAmAStateStoreAsync stateStore, 
        CancellationToken cancellationToken = default(CancellationToken)
        )
    {
        if (job is null)
            return;
        
        if (commandProcessor is null)
            throw new ArgumentNullException(nameof(commandProcessor));

        var command = requestFactory(job.Data);

        command.CorrelationId = job.Id;
        
        job.AddPendingResponse(
            typeof(TReply), 
            new TaskResponse<TData>((reply, _) => replyFactory(reply as TReply, job.Data), 
                typeof(TReply), 
                typeof(TFault)
                )
            );
        job.AddPendingResponse(
            typeof(TFault), 
            new TaskResponse<TData>((reply, _) => faultFactory(reply as TFault, job.Data), 
                typeof(TReply), 
                typeof(TFault)
                )
            );
        await stateStore.SaveJobAsync(job, cancellationToken);
        
        await commandProcessor.SendAsync(command, cancellationToken: cancellationToken);
    }
}


