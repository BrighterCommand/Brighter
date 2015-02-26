using System;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;

namespace HelloWorld
{
    internal class GreetingCommandHandler : RequestHandler<GreetingCommand>
    {
        public GreetingCommandHandler(ILog logger) : base(logger) { }

        public override GreetingCommand Handle(GreetingCommand command)
        {
            Console.WriteLine("Hello {0}", command.Name);
            return command;
        }
    }
}
