using System;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyPreAndPostDecoratedEventHandler : RequestHandler<MyEvent>, IDisposable
    {
        private static MyEvent s_command;
        public static bool DisposeWasCalled { get; set; }

        public MyPreAndPostDecoratedEventHandler(ILog logger)
            : base(logger)
        {
            s_command = null;
            DisposeWasCalled = false;
        }

        [MyPreValidationHandler(step: 2, timing: HandlerTiming.Before)]
        [MyPostLoggingHandler(step: 1, timing: HandlerTiming.After)]
        public override MyEvent Handle(MyEvent command)
        {
            LogCommand(command);
            return base.Handle(command);
        }

        public static bool Shouldreceive(MyEvent expectedCommand)
        {
            return (s_command != null) && (expectedCommand.Id == s_command.Id);
        }

        private void LogCommand(MyEvent request)
        {
            s_command = request;
        }

        public void Dispose()
        {
            DisposeWasCalled = true;
        }
    }
}