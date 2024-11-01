using System;

namespace Paramore.Brighter.Workflow;

public class FireAndForgetAction<TRequest>(Func<WorkflowState, TRequest> makeCommand) : IWorkflowAction where TRequest : class, IRequest
{
    public void Handle(WorkflowState state, IAmACommandProcessor commandProcessor)
    {
        commandProcessor.Send(makeCommand(state));
    }
}
