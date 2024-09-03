using System;
using Paramore.Brighter;

namespace Greetings.Ports.Commands
{
    public class AddGreetingCommand : Command
    {
        public AddGreetingCommand() : base(Guid.NewGuid()) { }

        public string GreetingMessage { get; set; } = "Hello Paul.";

        public bool ThrowError { get; set; } = false;
    }
}
