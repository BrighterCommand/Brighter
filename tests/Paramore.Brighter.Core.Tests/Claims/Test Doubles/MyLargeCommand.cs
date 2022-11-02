using System;

namespace Paramore.Brighter.Core.Tests.Claims.Test_Doubles;

public class MyLargeCommand : Command
{
    public string Value { get; set; }

    public MyLargeCommand() : this(0) { /* requires a default constructor to deserialize*/ }
    public MyLargeCommand(int valueLength) : base(Guid.NewGuid())
    {
        Value = DataGenerator.CreateString(valueLength);
    }
}
