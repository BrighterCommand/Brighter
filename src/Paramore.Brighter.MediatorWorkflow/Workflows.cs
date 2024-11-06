using System;

namespace Paramore.Brighter.MediatorWorkflow;

/// <summary>
/// A step in the worfklow. Steps form a singly linked list.
/// </summary>
/// <param name="Name">The name of the step</param>
/// <param name="Action">The type of action we take with the step</param>
/// <param name="Flow">The workflow that we belong to</param>
/// <param name="Next">What is the next step in sequence</param>
public record Step<TData>(string Name, IWorkflowAction<TData> Action, Action OnCompletion, Workflow<TData> Flow, Step<TData>? Next) where TData : IAmTheWorkflowData;

public interface IWorkflowAction<TData> where TData : IAmTheWorkflowData
{
    void Handle(Workflow<TData> state, IAmACommandProcessor commandProcessor);
}

public class FireAndForgetAction<TRequest, TData>(Func<TRequest> requestFactory) : IWorkflowAction<TData> where TRequest : class, IRequest where TData : IAmTheWorkflowData
{
    public void Handle(Workflow<TData> state, IAmACommandProcessor commandProcessor)
    {
        var command = requestFactory();
        command.CorrelationId = state.Id;
        commandProcessor.Send(command);
    }
}

public class RequestAndReplyAction<TRequest, TReply, TData>(Func<TRequest> requestFactory, Action<Event> replyFactory) 
    : IWorkflowAction<TData> where TRequest : class, IRequest where TReply : class, IRequest where TData : IAmTheWorkflowData
{
    public void Handle(Workflow<TData> state, IAmACommandProcessor commandProcessor)
    {
        var command = requestFactory();
        command.CorrelationId = state.Id;
        commandProcessor.Send(command);
       
        state.PendingResponses.Add(typeof(TReply), (reply, _) => replyFactory(reply));
    }
}
