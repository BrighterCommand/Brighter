namespace paramore.commandprocessor
{
    public interface IAmAPipelineBuilder<TRequest> where TRequest : class, IRequest
    {
        ChainOfResponsibility<TRequest> Build(IRequestContext requestContext);
    }
}