using System;

namespace Paramore.Brighter.AspNetCore
{
    public class AspNetHandlerFactory : IAmAHandlerFactory, IAmAHandlerFactoryAsync
    {
        private readonly IServiceProvider _serviceProvider;

        public AspNetHandlerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        IHandleRequests IAmAHandlerFactory.Create(Type handlerType)
        {
            return (IHandleRequests)_serviceProvider.GetService(handlerType);
        }

        IHandleRequestsAsync IAmAHandlerFactoryAsync.Create(Type handlerType)
        {
            return (IHandleRequestsAsync)_serviceProvider.GetService(handlerType);
        }

        public void Release(IHandleRequests handler)
        {
            var diposal = handler as IDisposable;
            diposal?.Dispose();
        }

        public void Release(IHandleRequestsAsync handler)
        {
            var diposal = handler as IDisposable;
            diposal?.Dispose();
        }
    }
}