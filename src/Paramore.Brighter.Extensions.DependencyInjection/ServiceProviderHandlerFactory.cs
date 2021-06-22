using System;
using System.Collections.Generic;

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

        public bool TryReleaseScope(IEnumerable<IHandleRequestsAsync> handleRequestsAsync) => false;
        public bool TryReleaseScope(IEnumerable<IHandleRequests> handleRequestsList) => false;
        public bool TryCreateScope(IAmALifetime instanceScope) => false;
    }
}
