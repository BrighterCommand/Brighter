using System;

namespace Paramore.Brighter.RMQ.Async.Tests.TestDoubles;

internal sealed class QuickHandlerFactory(Func<IHandleRequests> handlerAction) : IAmAHandlerFactorySync
{
    public IHandleRequests Create(Type handlerType, IAmALifetime lifetime)
    {
        return handlerAction();
    }

    public void Release(IHandleRequests handler, IAmALifetime lifetime) { }
}
