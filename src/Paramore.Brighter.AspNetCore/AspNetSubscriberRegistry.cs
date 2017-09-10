using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.AspNetCore
{
    internal class AspNetSubscriberRegistry : IAmASubscriberRegistry, IAmAnAsyncSubcriberRegistry
    {
        private readonly IServiceCollection _services;
        private readonly SubscriberRegistry _registry;

        public AspNetSubscriberRegistry(IServiceCollection services)
        {
            _services = services;
            _registry = new SubscriberRegistry();
        }

        public IEnumerable<Type> Get<T>() where T : class, IRequest
        {
            return _registry.Get<T>();
        }

        public void Register<TRequest, TImplementation>()
            where TRequest : class, IRequest
            where TImplementation : class, IHandleRequests<TRequest>
        {
            _services.AddTransient<TImplementation>();
            _registry.Register<TRequest, TImplementation>();
        }

        public void RegisterAsync<TRequest, TImplementation>()
            where TRequest : class, IRequest
            where TImplementation : class, IHandleRequestsAsync<TRequest>
        {
            _services.AddTransient<TImplementation>();
            _registry.RegisterAsync<TRequest, TImplementation>();
        }

        public void Add(Type requestType, Type handlerType)
        {
            _services.AddTransient(handlerType);
            _registry.Add(requestType, handlerType);
        }
    }
}