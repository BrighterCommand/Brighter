using System;

namespace Paramore.Brighter.AWSSQS.Tests.TestDoubles
{
    internal class QuickHandlerFactory : IAmAHandlerFactorySync
    {
        private readonly Func<IHandleRequests> _handlerAction;

        public QuickHandlerFactory(Func<IHandleRequests> handlerAction)
        {
            _handlerAction = handlerAction;
        }
        public IHandleRequests Create(Type handlerType)
        {
            return _handlerAction();
        }

        public void Release(IHandleRequests handler) { }
    }
}
