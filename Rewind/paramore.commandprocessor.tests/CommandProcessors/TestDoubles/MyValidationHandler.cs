namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyValidationHandler<TRequest> : RequestHandler<TRequest> where TRequest : class, IRequest
    {
        public override TRequest Handle(TRequest command)
        {
            return command;
        }
    }
}