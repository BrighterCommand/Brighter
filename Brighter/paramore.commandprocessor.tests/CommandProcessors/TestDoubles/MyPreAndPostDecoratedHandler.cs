using System;
using paramore.brighter.commandprocessor;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyPreAndPostDecoratedHandler : RequestHandler<MyCommand>, IDisposable
    {
        private static MyCommand command;
        public static bool DisposeWasCalled { get; set; }

        public MyPreAndPostDecoratedHandler()
        {
            command = null;
            DisposeWasCalled = false;
        }

        [MyPreValidationHandlerAttribute(step: 2, timing: HandlerTiming.Before)]
        [MyPostLoggingHandlerAttribute(step: 1, timing: HandlerTiming.After)]
        public override MyCommand Handle(MyCommand command)
        {
            LogCommand(command);
            return base.Handle(command);
        }

        public static bool ShouldRecieve(MyCommand expectedCommand)
        {
            return (command != null) && (expectedCommand.Id == command.Id);
        }

        private void LogCommand(MyCommand request)
        {
            command = request;
        }

        public void Dispose()
        {
            DisposeWasCalled = true;
        }
    }
}