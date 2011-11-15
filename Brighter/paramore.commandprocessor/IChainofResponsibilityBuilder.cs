namespace paramore.commandprocessor
{
    public interface IChainofResponsibilityBuilder<TRequest> where TRequest : class, IRequest
    {
        Chains<TRequest> Build();
    }
}