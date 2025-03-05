using System;

namespace Paramore.Brighter.RMQ.Async.Tests.TestDoubles;

internal sealed class QuickHandlerFactoryAsync(Func<IHandleRequestsAsync> handlerAction) : IAmAHandlerFactoryAsync
{
    public IHandleRequestsAsync Create(Type handlerType, IAmALifetime lifetime)
    {
        return handlerAction();
    }

    public void Release(IHandleRequestsAsync handler, IAmALifetime lifetime) { }
}
