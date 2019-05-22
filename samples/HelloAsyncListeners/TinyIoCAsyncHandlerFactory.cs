using System;
using Paramore.Brighter;
using TinyIoC;

namespace HelloAsyncListeners
{
    internal class TinyIocHandlerFactory : IAmAHandlerFactoryAsync
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

        IHandleRequestsAsync IAmAHandlerFactoryAsync.Create(Type handlerType)
        {
            return (IHandleRequestsAsync)_container.Resolve(handlerType);
        }
    }
}
