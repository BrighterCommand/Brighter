using System;

namespace Paramore.Brighter.Workflow;

public class FireAndForgetAction<TRequest>(Func<WorkflowState, TRequest> requestFactory) : IWorkflowAction where TRequest : class, IRequest
{
    public void Handle(WorkflowState state, IAmACommandProcessor commandProcessor)
    {
        commandProcessor.Send(requestFactory(state));
    }
}
