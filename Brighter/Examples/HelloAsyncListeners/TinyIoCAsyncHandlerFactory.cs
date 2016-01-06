using System;
using paramore.brighter.commandprocessor;
using TinyIoC;

namespace HelloAsyncListeners
{
    internal class TinyIocHandlerFactory : IAmAnAsyncHandlerFactory
    {
        private readonly TinyIoCContainer _container;

        public TinyIocHandlerFactory(TinyIoCContainer container)
        {
            _container = container;
        }

        public void Release(IHandleRequestsAsync handler)
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            var disposable = handler as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }

        IHandleRequestsAsync IAmAnAsyncHandlerFactory.Create(Type handlerType)
        {
            return (IHandleRequestsAsync)_container.Resolve(handlerType);
        }
    }
}
