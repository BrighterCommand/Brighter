using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// .NET IoC backed Subscriber registry, used to find matching handlers
    /// </summary>
    public class ServiceCollectionSubscriberRegistry : IAmASubscriberRegistry, IAmAnAsyncSubcriberRegistry
    {
        private readonly IServiceCollection _services;
        private readonly SubscriberRegistry _registry;
        private ServiceLifetime _lifetime;

        /// <summary>
        /// Constructs an instance of the subscriber registry
        /// We set the lifetime for registered handlers here. We default to transient i.e. the handler will be destroyed after usage
        /// In contexts using a Scope, for example ASP.NET with EF Core, this will need to be Scoped to allow participation in Request-Reply scoped
        /// transactions etc.
        /// </summary>
        /// <param name="services">The IoC container to register with</param>
        /// <param name="lifetime">The lifetime of a handler - defaults to transient</param>
        public ServiceCollectionSubscriberRegistry(IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Transient)
        {
            _services = services;
            _registry = new SubscriberRegistry();
            _lifetime = lifetime;
        }

        /// <summary>
        /// Get the matching set of handlers for a request type
        /// </summary>
        /// <typeparam name="T">The type of request</typeparam>
        /// <returns>An iterator over a set of registered handlers for that type</returns>
        public IEnumerable<Type> Get<T>() where T : class, IRequest
        {
            return _registry.Get<T>();
        }

        /// <summary>
        /// Register a handler type for a request type
        /// Registers with IoC container :-
        ///  - the handler type (TImplementation); allows factory instantiation of request handler
        /// </summary>
        /// <typeparam name="TRequest">The handler</typeparam>
        /// <typeparam name="TImplementation">The type of the handler for this request type</typeparam>
        public void Register<TRequest, TImplementation>()
            where TRequest : class, IRequest
            where TImplementation : class, IHandleRequests<TRequest>
        {
            _services.Add(new ServiceDescriptor(typeof(TImplementation), typeof(TImplementation), _lifetime));
            _registry.Register<TRequest, TImplementation>();
        }

        /// <summary>
        /// Register a handler type for a request type
        /// Registers with IoC container :-
        ///  - the handler type (TImplementation); allows factory instantiation of request handler
        /// </summary>
        /// <typeparam name="TRequest">The handler</typeparam>
        /// <typeparam name="TImplementation">The type of the handler for this request type</typeparam>
        public void RegisterAsync<TRequest, TImplementation>()
            where TRequest : class, IRequest
            where TImplementation : class, IHandleRequestsAsync<TRequest>
        {
            _services.Add(new ServiceDescriptor(typeof(TImplementation), typeof(TImplementation), _lifetime));
            _registry.RegisterAsync<TRequest, TImplementation>();
        }

        /// <summary>
        /// Register a handler type for a request type
        /// Registers with IoC container :-
        ///  - the handler type (TImplementation); allows factory instantiation of request handler
        /// </summary>
        /// <param name="requestType">The handler</param>
        /// <param name="handlerType">The type of the handler for this request type</param>
        public void Add(Type requestType, Type handlerType)
        {
            _services.Add(new ServiceDescriptor(handlerType, handlerType, _lifetime));
            _registry.Add(requestType, handlerType);
        }
    }
}
