using System;
using Common.Logging;
using paramore.brighter.commandprocessor;

namespace HelloWorld
{
    class GreetingCommandHandler : RequestHandler<GreetingCommand>
    {
        public GreetingCommandHandler(ILog logger) : base(logger) {}

        public override GreetingCommand Handle(GreetingCommand command)
        {
            Console.WriteLine("Hello {0}", command.Name);
            return command;
        }
    }
}
