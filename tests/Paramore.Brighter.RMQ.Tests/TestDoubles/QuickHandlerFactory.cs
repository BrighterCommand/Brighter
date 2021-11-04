using System;
using Paramore.Brighter.Scope;

namespace Paramore.Brighter.RMQ.Tests.TestDoubles
{
    internal class QuickHandlerFactory : IAmAHandlerFactorySync
    {
        private readonly Func<IHandleRequests> _handlerAction;

        public QuickHandlerFactory(Func<IHandleRequests> handlerAction)
        {
            _handlerAction = handlerAction;
        }
        public IHandleRequests Create(Type handlerType, IAmALifetime lifetimeScope)
        {
            return _handlerAction();
        }

        public void Release(IHandleRequests handler) { }

        public IBrighterScope CreateScope() => new Unscoped();
    }
}
