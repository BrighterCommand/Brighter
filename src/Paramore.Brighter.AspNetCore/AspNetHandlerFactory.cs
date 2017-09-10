using System;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.AspNetCore
{
    internal class AspNetHandlerFactory : IAmAHandlerFactory, IAmAHandlerFactoryAsync
    {
        private readonly Lazy<IServiceProvider> _serviceProvider;

        public AspNetHandlerFactory(IServiceCollection services)
        {
            _serviceProvider = new Lazy<IServiceProvider>(services.BuildServiceProvider);
        }

        IHandleRequests IAmAHandlerFactory.Create(Type handlerType)
        {
            return (IHandleRequests)_serviceProvider.Value.GetService(handlerType);
        }

        IHandleRequestsAsync IAmAHandlerFactoryAsync.Create(Type handlerType)
        {
            return (IHandleRequestsAsync)_serviceProvider.Value.GetService(handlerType);
        }

        public void Release(IHandleRequests handler)
        {
            // no op
        }

        public void Release(IHandleRequestsAsync handler)
        {
            // no op
        }
    }
}