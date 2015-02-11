using System;
using Greetings.Ports.Commands;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;

namespace Greetings.Ports.CommandHandlers
{
    internal class GreetingCommandHandler : RequestHandler<GreetingCommand>
    {

        public GreetingCommandHandler(ILog logger) : base(logger) {}

        public override GreetingCommand Handle(GreetingCommand command)
        {
            Console.WriteLine("Recieved Greeting. Message Follows");
            Console.WriteLine("----------------------------------");
            Console.WriteLine(command.Greeting);
            Console.WriteLine("----------------------------------");
            Console.WriteLine("Message Ends");
            return base.Handle(command);
        }

    }
}
