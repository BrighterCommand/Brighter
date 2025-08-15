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

        public static ASBTestCommand BuildLarge()
        {
            const int FixedSize = 2048000; //2MB

            return new ASBTestCommand()
            {
                CommandValue = Guid.NewGuid().ToString().PadRight(FixedSize, 'a'), 
                CommandNumber = FixedSize
            };
        }

        public static ASBTestCommand BuildSmall()
        {
            const int FixedSize = 100;

            return new ASBTestCommand()
            {
                CommandValue = Guid.NewGuid().ToString().PadRight(FixedSize, 'a'),
                CommandNumber = FixedSize
            };
        }
    }
}
