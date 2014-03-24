using System;
using paramore.brighter.commandprocessor;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyLoggingHandler<TRequest> : RequestHandler<TRequest>, IDisposable where TRequest : class, IRequest
    {
        private TRequest command;
        public static bool DisposeWasCalled { get; set; }

        public MyLoggingHandler()
        {
            command = null;
            DisposeWasCalled = false;
        }

        public override TRequest Handle(TRequest command)
        {
            LogCommand(command);
            return base.Handle(command);
        }
        
        public static bool ShouldRecieve(TRequest expectedCommand)
        {
            return (expectedCommand != null);
        }

        private void LogCommand(TRequest request)
        {
            command = request;
        }

        public void Dispose()
        {
            DisposeWasCalled = true;
        }
    }
}