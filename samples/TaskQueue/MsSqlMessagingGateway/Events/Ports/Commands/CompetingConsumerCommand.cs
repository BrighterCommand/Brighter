using System;
using Paramore.Brighter;

namespace Events.Ports.Commands
{
    public class CompetingConsumerCommand(int commandNumber) : Command(Id.Random())
    {
        public int CommandNumber { get; } = commandNumber;
    }
}
