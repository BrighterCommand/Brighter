using System;
using Paramore.Brighter;

public class QuickHandlerFactoryAsync : IAmAHandlerFactoryAsync
{
    private readonly Func<IHandleRequestsAsync> _handlerFactory;

    public QuickHandlerFactoryAsync(Func<IHandleRequestsAsync> handlerFactory)
    {
        _handlerFactory = handlerFactory;
    }

    public IHandleRequestsAsync Create(Type handlerType)
    {
        return _handlerFactory();
    }

    public void Release(IHandleRequestsAsync handler)
    {
        // Implement any necessary cleanup logic here
    }
}
