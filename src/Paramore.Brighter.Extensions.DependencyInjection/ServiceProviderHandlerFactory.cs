using System;

namespace Paramore.Brighter.Extensions.DependencyInjection
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
            return (IHandleRequests)_serviceProvider.GetService(handlerType);
        }

        IHandleRequestsAsync IAmAHandlerFactoryAsync.Create(Type handlerType)
        {
            return (IHandleRequestsAsync)_serviceProvider.GetService(handlerType);
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