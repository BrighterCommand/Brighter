﻿using System.Collections.Generic;
using Paramore.Brighter.Mediator;

namespace Paramore.Brighter.Core.Tests.Specifications.TestDoubles;

public enum TestState
{
    Done,
    Ready,
    Running,
    Waiting
}

public class SpecificationTestState 
{
    public TestState State { get; set; }
    public Dictionary<string, object> Bag { get; set; }
}
