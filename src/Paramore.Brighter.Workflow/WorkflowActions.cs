using System;

namespace Paramore.Brighter.Workflow;

public interface IWorkflowAction
{
    void Handle(WorkflowState state, IAmACommandProcessor commandProcessor);
}

public class FireAndForgetAction<TRequest>(Func<WorkflowState, TRequest> requestFactory) : IWorkflowAction where TRequest : class, IRequest
{
    public void Handle(WorkflowState state, IAmACommandProcessor commandProcessor)
    {
        commandProcessor.Send(requestFactory(state));
    }
}

public class RequestAndReplyAction<TRequest, TReply>(Func<WorkflowState, TRequest> requestFactory, Action<Event, WorkflowState> replyFactory) 
    : IWorkflowAction where TRequest : class, IRequest where TReply : class, IRequest
{
    public void Handle(WorkflowState state, IAmACommandProcessor commandProcessor)
    {
        commandProcessor.Send(requestFactory(state));
       
        state.PendingResponses.Add(typeof(TReply), (reply, state) => replyFactory(reply, state));
    }
}
