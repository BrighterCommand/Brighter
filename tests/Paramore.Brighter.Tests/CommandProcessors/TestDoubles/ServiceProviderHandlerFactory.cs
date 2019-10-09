using System;

namespace Paramore.Brighter.Tests.CommandProcessors.TestDoubles
{
    public class ServiceProviderHandlerFactory : IAmAHandlerFactory, IAmAHandlerFactoryAsync
    {
        private readonly IServiceProvider _serviceProvider;

        public ServiceProviderHandlerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        IHandleRequests IAmAHandlerFactory.Create(Type handlerType)
        {
            var handleRequests = (IHandleRequests)_serviceProvider.GetService(handlerType);
            return handleRequests;
        }

        IHandleRequestsAsync IAmAHandlerFactoryAsync.Create(Type handlerType)
        {
            var handleRequestsAsync = (IHandleRequestsAsync)_serviceProvider.GetService(handlerType);
            return handleRequestsAsync;
        }

        public void Release(IHandleRequests handler)
        {
            var disposal = handler as IDisposable;
            disposal?.Dispose();
        }

        public void Release(IHandleRequestsAsync handler)
        {
            var disposal = handler as IDisposable;
            disposal?.Dispose();
        }
    }
}
