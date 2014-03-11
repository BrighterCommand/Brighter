namespace paramore.commandprocessor.policy.Handlers
{
    class PolicyHandler<TRequest> : RequestHandler<TRequest> where TRequest : class, IRequest
    {
    }
}
