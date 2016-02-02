using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyLogWritingCommandHandler : RequestHandler<MyLogWritingCommand>
    {
        private readonly string _handlerLogMessage;
        private static MyLogWritingCommand s_command;

        public MyLogWritingCommandHandler(string handlerLogMessage)
        {
            _handlerLogMessage = handlerLogMessage;
        }

        public override MyLogWritingCommand Handle(MyLogWritingCommand command)
        {
            s_command = command;
            Logger.Log(LogLevel.Debug, () => _handlerLogMessage);

            return base.Handle(command);
        }

        public static bool Shouldreceive(MyLogWritingCommand expectedCommand)
        {
            return (s_command != null) && (expectedCommand.Id == s_command.Id);
        }

    }
}