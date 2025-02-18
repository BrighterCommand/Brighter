using System;

namespace Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;

internal class QuickHandlerFactory(Func<IHandleRequests> handlerAction) : IAmAHandlerFactorySync
{
    public IHandleRequests Create(Type handlerType, IAmALifetime lifetime)
    {
        return handlerAction();
    }

    public void Release(IHandleRequests handler, IAmALifetime lifetime) { }
}
