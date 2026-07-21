using System.Collections.Generic;

namespace Paramore.Brighter.Core.Tests.Specifications.TestDoubles;

public enum SpecificationState
{
    Done,
    Ready,
    Running,
    Waiting
}

public class SpecificationTestState 
{
    public SpecificationState State { get; set; }
    public Dictionary<string, object> Bag { get; set; }
}
