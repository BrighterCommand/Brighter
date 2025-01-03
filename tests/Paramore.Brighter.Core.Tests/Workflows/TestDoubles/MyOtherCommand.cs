using System;

namespace Paramore.Brighter.Core.Tests.Workflows.TestDoubles;

public class MyOtherCommand() : Command(Guid.NewGuid().ToString())
{
    public string Value { get; set; } = string.Empty;
}
