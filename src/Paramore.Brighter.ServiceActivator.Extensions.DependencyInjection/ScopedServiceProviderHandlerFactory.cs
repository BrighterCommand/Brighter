using System;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection
{
    public class ScopedServiceProviderHandlerFactory : IAmAHandlerFactorySync, IAmAHandlerFactoryAsync
    {
        private readonly IServiceProvider _serviceProvider;

        public ScopedServiceProviderHandlerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IHandleRequests Create(Type handlerType, IAmALifetime lifetimeScope)
        {
            var handleRequests = (IHandleRequests)lifetimeScope.Scope.ServiceProvider.GetService(handlerType);
            return handleRequests;
        }

        IHandleRequestsAsync IAmAHandlerFactoryAsync.Create(Type handlerType, IAmALifetime lifetimeScope)
        {
            var handleRequests = (IHandleRequestsAsync)lifetimeScope.Scope.ServiceProvider.GetService(handlerType);
            return handleRequests;
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

        public IServiceScope CreateScope()
        {
            return _serviceProvider.CreateScope();
        }
    }
}
