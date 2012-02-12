namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyLoggingHandler<TRequest> : RequestHandler<TRequest> where TRequest : class, IRequest
    {
        private TRequest command;

        public MyLoggingHandler()
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
            return (expectedCommand != null);
        }

        private void LogCommand(TRequest request)
        {
            command = request;
        }
    }
}