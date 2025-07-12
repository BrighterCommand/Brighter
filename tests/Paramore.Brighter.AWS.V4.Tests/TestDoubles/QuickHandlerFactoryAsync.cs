using System;

namespace Paramore.Brighter.AWS.V4.Tests.TestDoubles;

public class QuickHandlerFactoryAsync : IAmAHandlerFactoryAsync
{
    private readonly Func<IHandleRequestsAsync> _handlerFactory;

    public QuickHandlerFactoryAsync(Func<IHandleRequestsAsync> handlerFactory)
    {
        _handlerFactory = handlerFactory;
    }

    public IHandleRequestsAsync Create(Type handlerType, IAmALifetime lifetime)
    {
        return _handlerFactory();
    }

    public void Release(IHandleRequestsAsync handler, IAmALifetime lifetime)
    {
        // Implement any necessary cleanup logic here
    }
}
