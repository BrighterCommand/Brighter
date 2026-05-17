using System;

namespace Paramore.Brighter.Core.Tests.Validation.TestDoubles;

public class MyDescribableCommand : Command
{
    public string Value { get; set; } = "Test";

    public MyDescribableCommand() : base(Guid.NewGuid()) { }
}
