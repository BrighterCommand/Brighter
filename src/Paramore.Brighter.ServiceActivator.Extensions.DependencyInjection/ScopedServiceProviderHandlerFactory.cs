using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Scope;

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
            var serviceScope = GetServiceProviderFromScope(lifetimeScope);
            var handleRequests = (IHandleRequests)serviceScope.GetService(handlerType);
            return handleRequests;
        }

        IHandleRequestsAsync IAmAHandlerFactoryAsync.Create(Type handlerType, IAmALifetime lifetimeScope)
        {
            var serviceScope = GetServiceProviderFromScope(lifetimeScope);
            var handleRequests = (IHandleRequestsAsync)serviceScope.GetService(handlerType);
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

        public IBrighterScope CreateScope()
        {
            return new ServiceProviderScope(_serviceProvider.CreateScope());
        }
        private static IServiceProvider GetServiceProviderFromScope(IAmALifetime lifetimeScope)
        {
            var serviceScope = (ServiceProviderScope)lifetimeScope.Scope;
            return serviceScope.ServiceProvider;
        }
    }

    public class ServiceProviderScope : IBrighterScope
    {
        private readonly IServiceScope _scope;

        public ServiceProviderScope(IServiceScope scope)
        {
            _scope = scope;
        }

        public IServiceProvider ServiceProvider => _scope.ServiceProvider;

        public void Dispose()
        {
            _scope?.Dispose();
        }
    }
}
;
