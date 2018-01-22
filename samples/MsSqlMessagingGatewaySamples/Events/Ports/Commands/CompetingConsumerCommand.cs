using System;
using Paramore.Brighter;

namespace Events.Ports.Commands
{
    public class CompetingConsumerCommand : Command
    {
        public CompetingConsumerCommand(int commandNumber) : base(Guid.NewGuid())
        {
            CommandNumber = commandNumber;
        }

        public int CommandNumber { get; }
    }
}