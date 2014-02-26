namespace paramore.commandprocessor
{
    public interface IAmAPipelineBuilder<TRequest> where TRequest : class, IRequest
    {
        Pipelines<TRequest> Build(IRequestContext requestContext);
    }
}