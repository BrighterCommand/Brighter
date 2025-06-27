using System;
using Paramore.Brighter.AWS.V4.Tests.Helpers;

namespace Paramore.Brighter.AWS.V4.Tests.TestDoubles
{
    public class MyLargeCommand : Command
    {
        public string Value { get; set; }

        public MyLargeCommand() : this(0) { /* requires a default constructor to deserialize*/ }
        public MyLargeCommand(int valueLength) : base(Guid.NewGuid())
        {
            Value = DataGenerator.CreateString(valueLength);
        }
    }
}
