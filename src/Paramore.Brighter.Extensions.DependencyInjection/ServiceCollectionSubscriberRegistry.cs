#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
 
#endregion


using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// .NET IoC backed Subscriber registry, used to find matching handlers
    /// </summary>
    public class ServiceCollectionSubscriberRegistry : IAmASubscriberRegistry, IAmAnAsyncSubcriberRegistry
    {
        private readonly IServiceCollection _services;
        private readonly SubscriberRegistry _registry;
        private readonly ServiceLifetime _lifetime;

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
        /// Register a handler type for a request type
        /// Registers with IoC container :-
        ///  - the handler type (TImplementation); allows factory instantiation of request handler
        /// </summary>
        /// <remarks>Mainly intended for internal use, prefer to use <see cref="Register{TRequest,TImplementation}"/></remarks>
        /// <param name="requestType">The handler</param>
        /// <param name="handlerType">The type of the handler for this request type</param>
        public void Add(Type requestType, Type handlerType)
        {
            _services.TryAdd(new ServiceDescriptor(handlerType, handlerType, _lifetime));
            _registry.Add(requestType, handlerType);
        }

        /// <summary>
        /// Adds a type to the registry, with a mapping to a single handler. We will add this to any existing handlers for this type
        /// </summary>
        /// <remarks>Mainly intended for internal use, prefer to use <see cref="Register{TRequest}"/></remarks>
        /// <param name="requestType">The <see cref="IRequest"/> type that will be sent/published</param>
        /// <param name="router">The routing function that takes a request and a request context and returns one or more handlers.</param>
        /// <param name="handlerTypes">We need a <see cref="IEnumerable{T}"/> of <see cref="Type"/> that router can return to register with the factory</param>
        public void Add(Type requestType, Func<IRequest?, IRequestContext?, List<Type>> router, IEnumerable<Type>handlerTypes)
        {
            handlerTypes.Each(handlerType =>
            {
                _services.TryAdd(new ServiceDescriptor(handlerType, handlerType, _lifetime));
            });
            _registry.Add(requestType, router, handlerTypes); 
        }

        /// <summary>
        /// Get the matching set of handlers for a request type
        /// </summary>
        /// <typeparam name="T">The type of request</typeparam>
        /// <returns>An iterator over a set of registered handlers for that type</returns>
        public IEnumerable<Type> Get<T>(T request, IRequestContext requestContext) where T : class, IRequest
        {
            return _registry.Get(request, requestContext);
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
            _services.TryAdd(new ServiceDescriptor(typeof(TImplementation), typeof(TImplementation), _lifetime));
            _registry.Register<TRequest, TImplementation>();
        }
        
        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the t request.</typeparam>        
        /// <param name="router">The routing function that takes a request and a request context and returns one or more handlers.</param>
        /// <param name="handlerTypes">We need a <see cref="IEnumerable{T}"/> of <see cref="Type"/> that router can return to register with the factory</param>
        public  void Register<TRequest>(Func<IRequest?, IRequestContext?, List<Type>> router, IEnumerable<Type> handlerTypes)
        {
            handlerTypes.Each(handlerType =>
            {
                _services.TryAdd(new ServiceDescriptor(handlerType, handlerType, _lifetime));
            });
           _registry.Register<TRequest>(router, handlerTypes);
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
            _services.TryAdd(new ServiceDescriptor(typeof(TImplementation), typeof(TImplementation), _lifetime));
            _registry.RegisterAsync<TRequest, TImplementation>();
        }

        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the t request.</typeparam>        
        /// <param name="router">The routing function that takes a request and a request context and returns one or more handlers.</param>
        /// <param name="handlerTypes">We need a <see cref="IEnumerable{T}"/> of <see cref="Type"/> that router can return to register with the factory</param>
        public void RegisterAsync<TRequest>(Func<IRequest?, IRequestContext?, List<Type>> router, IEnumerable<Type> handlerTypes)
        {
            handlerTypes.Each(handlerType =>
            {
                _services.TryAdd(new ServiceDescriptor(handlerType, handlerType, _lifetime));
            });
            _registry.RegisterAsync<TRequest>(router, handlerTypes); 
        }
    }
}
