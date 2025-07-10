using System;

namespace Paramore.Brighter.Gcp.Tests.TestDoubles;

internal sealed class QuickHandlerFactory : IAmAHandlerFactorySync
{
    private readonly Func<IHandleRequests> _handlerAction;

    public QuickHandlerFactory(Func<IHandleRequests> handlerAction)
    {
        _handlerAction = handlerAction;
    }
    public IHandleRequests Create(Type handlerType, IAmALifetime lifetime)
    {
        return _handlerAction();
    }

    public void Release(IHandleRequests handler, IAmALifetime lifetime) { }
}