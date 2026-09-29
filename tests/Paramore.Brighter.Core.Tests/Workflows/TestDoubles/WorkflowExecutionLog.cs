using System.Collections.Generic;

namespace Paramore.Brighter.Core.Tests.Workflows.TestDoubles;

internal sealed class WorkflowExecutionLog
{
    public List<MyCommand> Commands { get; } = [];

    public List<MyOtherCommand> OtherCommands { get; } = [];

    public List<MyEvent> Events { get; } = [];

    public List<MyFault> Faults { get; } = [];
}
