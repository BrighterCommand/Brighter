using System.Collections.Concurrent;

namespace Paramore.Brighter.Core.Tests.Workflows.TestDoubles;

public class WorkflowTestData 
{
    public ConcurrentDictionary<string, object> Bag { get; set; } = new();
}
