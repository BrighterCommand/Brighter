using System.Collections.Generic;
using Paramore.Brighter.MediatorWorkflow;

namespace Paramore.Brighter.Core.Tests.Workflows.TestDoubles;

public class WorkflowTestData : IAmTheWorkflowData
{
    public Dictionary<string, object> Bag { get; set; } = new();
}
