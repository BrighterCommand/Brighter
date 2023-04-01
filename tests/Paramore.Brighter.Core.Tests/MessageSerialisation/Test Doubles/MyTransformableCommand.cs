using System;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

public class MyTransformableCommand : Command
{
    public string Value { get; set; } = "Test Value";

    public MyTransformableCommand() : base(Guid.NewGuid())
    {
    }
}
