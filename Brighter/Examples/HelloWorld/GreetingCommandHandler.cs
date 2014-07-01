using Common.Logging;
using paramore.brighter.commandprocessor;

namespace HelloWorld
{
    class GreetingCommandHandler : RequestHandler<GreetingCommand>
    {
        public GreetingCommandHandler(ILog logger) : base(logger) {}

        public GreetingCommand Handle(GreetingCommand command)
        {
        }
    }
}
