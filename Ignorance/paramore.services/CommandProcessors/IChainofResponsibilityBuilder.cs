using Paramore.Services.Common;

namespace Paramore.Services.CommandProcessors
{
    public interface IChainofResponsibilityBuilder<TRequest> where TRequest : class, IRequest
    {
        Chains<TRequest> Build();
    }
}