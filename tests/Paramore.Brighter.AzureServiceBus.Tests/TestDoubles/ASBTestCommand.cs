using System;

namespace Paramore.Brighter.AzureServiceBus.Tests.TestDoubles
{
    public class ASBTestCommand : Command
    {
        public ASBTestCommand() : base(Guid.NewGuid())
        {
        }

        public string CommandValue { get; set; }
        public int CommandNumber { get; set; }
    }
}
