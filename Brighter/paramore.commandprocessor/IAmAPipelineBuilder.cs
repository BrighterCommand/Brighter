using System;

namespace paramore.commandprocessor
{
    public interface IAmAPipelineBuilder<TRequest> : IDisposable where TRequest : class, IRequest 
    {
        Pipelines<TRequest> Build(IRequestContext requestContext);
    }
}