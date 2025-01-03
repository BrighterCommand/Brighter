using System;

namespace Paramore.Brighter.RMQ.Tests.TestDoubles;

internal class QuickHandlerFactory(Func<IHandleRequests> handlerAction) : IAmAHandlerFactorySync
{
    public IHandleRequests Create(Type handlerType)
    {
        return handlerAction();
    }

    public void Release(IHandleRequests handler) { }
}
