using System;

namespace Paramore.Brighter.MediatorWorkflow;

/// <summary>
/// A step in the worfklow. Steps form a singly linked list.
/// </summary>
/// <param name="Name">The name of the step</param>
/// <param name="Action">The type of action we take with the step</param>
/// <param name="Flow">The workflow that we belong to</param>
/// <param name="Next">What is the next step in sequence</param>
public record Step(string Name, IWorkflowAction Action, Action OnCompletion, Workflow Flow, Step? Next);

public interface IWorkflowAction
{
    void Handle(Workflow state, IAmACommandProcessor commandProcessor);
}

public class FireAndForgetAction<TRequest>(Func<TRequest> requestFactory) : IWorkflowAction where TRequest : class, IRequest
{
    public void Handle(Workflow state, IAmACommandProcessor commandProcessor)
    {
        commandProcessor.Send(requestFactory());
    }
}

public class RequestAndReplyAction<TRequest, TReply>(Func<TRequest> requestFactory, Action<Event> replyFactory) 
    : IWorkflowAction where TRequest : class, IRequest where TReply : class, IRequest
{
    public void Handle(Workflow state, IAmACommandProcessor commandProcessor)
    {
        commandProcessor.Send(requestFactory());
       
        state.PendingResponses.Add(typeof(TReply), (reply, state) => replyFactory(reply));
    }
}
