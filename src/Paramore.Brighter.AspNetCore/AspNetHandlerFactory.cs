using System;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.AspNetCore
{
    internal class AspNetHandlerFactory : IAmAHandlerFactory, IAmAHandlerFactoryAsync
    {
        private readonly IServiceCollection _services;

        public AspNetHandlerFactory(IServiceCollection services)
        {
            _services = services;
        }

        IHandleRequests IAmAHandlerFactory.Create(Type handlerType)
        {
            return (IHandleRequests)_services.BuildServiceProvider().GetService(handlerType);
        }

        IHandleRequestsAsync IAmAHandlerFactoryAsync.Create(Type handlerType)
        {
            return (IHandleRequestsAsync)_services.BuildServiceProvider().GetService(handlerType);
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