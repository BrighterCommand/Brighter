using paramore.brighter.commandprocessor;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyValidationHandler<TRequest> : RequestHandler<TRequest> where TRequest : class, IRequest
    {
        private static TRequest command;

        public MyValidationHandler()
        {
            command = null;
        }

        public override TRequest Handle(TRequest command)
        {
            LogCommand(command);
            return base.Handle(command);
        }

        public static bool ShouldRecieve(TRequest expectedCommand)
        {
            return (command != null);
        }

        private void LogCommand(TRequest request)
        {
            command = request;
        }
    }
}