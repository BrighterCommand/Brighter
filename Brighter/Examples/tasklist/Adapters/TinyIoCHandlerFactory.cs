using System;
using paramore.brighter.commandprocessor;
using TinyIoC;

namespace Tasklist.Adapters
{
    class TinyIocHandlerFactory : IAmAHandlerFactory
    {
        private readonly TinyIoCContainer container;

        public TinyIocHandlerFactory(TinyIoCContainer container)
        {
            this.container = container;
        }

        public IHandleRequests Create(Type handlerType)
        {
            return (IHandleRequests)container.Resolve(handlerType);
        }

        public void Release(IHandleRequests handler)
        {
            var disposable = handler as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }
    }
}
