using System;
using Paramore.Brighter;

namespace Greetings.Ports.Commands
{
    // This attribute is not required, it's just a demonstration
    [PublicationTopic("greeting.addGreetingCommand")]
    public class AddGreetingCommand() : Command(Id.Random)
    {
        public string GreetingMessage { get; set; } = "Hello Paul.";

        public bool ThrowError { get; set; } = false;
    }
}
