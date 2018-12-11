using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    public class ServiceCollectionSubscriberRegistry : IAmASubscriberRegistry, IAmAnAsyncSubcriberRegistry
    {
        private readonly IServiceCollection _services;
        private readonly SubscriberRegistry _registry;
        private readonly ServiceLifetime _lifetime;

        public ServiceCollectionSubscriberRegistry(IServiceCollection services, ServiceLifetime lifetime)
        {
            _services = services;
            _registry = new SubscriberRegistry();
            _lifetime = lifetime;
        }

        public IEnumerable<Type> Get<T>() where T : class, IRequest
        {
            return _registry.Get<T>();
        }

        public void Register<TRequest, TImplementation>()
            where TRequest : class, IRequest
            where TImplementation : class, IHandleRequests<TRequest>
        {
            _services.Add(new ServiceDescriptor(
                typeof(TImplementation), typeof(TImplementation), _lifetime));
            _registry.Register<TRequest, TImplementation>();
        }

        public void RegisterAsync<TRequest, TImplementation>()
            where TRequest : class, IRequest
            where TImplementation : class, IHandleRequestsAsync<TRequest>
        {
            _services.Add(new ServiceDescriptor(
                typeof(TImplementation), typeof(TImplementation), _lifetime));
            _registry.RegisterAsync<TRequest, TImplementation>();
        }

        public void Add(Type requestType, Type handlerType)
        {
            _services.Add(new ServiceDescriptor(handlerType, handlerType, _lifetime));
            _registry.Add(requestType, handlerType);
        }
    }
}