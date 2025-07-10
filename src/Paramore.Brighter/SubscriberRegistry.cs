#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections;
using System.Collections.Generic;

namespace Paramore.Brighter
{
    /// <summary>
    /// Class SubscriberRegistry.
    /// In order to map an <see cref="IHandleRequests"/> or an <see cref="IHandleRequestsAsync"/> 
    /// to a <see cref="Command"/> or an <see cref="Event"/> we need you to register 
    /// the association via the <see cref="SubscriberRegistry"/>
    /// The default implementation of <see cref="SubscriberRegistry"/> is usable in most instances 
    /// and this is provided for testing
    /// </summary>
    public class SubscriberRegistry : IAmASubscriberRegistry, IAmAnAsyncSubcriberRegistry,
        IEnumerable<KeyValuePair<Type, List<Func<IRequest?, IRequestContext?, List<Type>>>>>
    {
        private readonly Dictionary<Type, List<Func<IRequest?, IRequestContext?, List<Type>>>> _observers = new();
        
        /// <summary>
        /// Adds a type to the registry, with a mapping to a single handler. We will add this to any existing handlers for this type
        /// </summary>
        /// <remarks>Mainly intended for internal use, prefer to use <see cref="Register{TRequest,TImplementation}"/></remarks>
        /// <param name="requestType">The <see cref="IRequest"/> type that will be sent/published</param>
        /// <param name="handlerType">The <see cref="IHandleRequests"/> type that will handle the request </param>
        public void Add(Type requestType, Type handlerType)
        {
            if (!_observers.TryGetValue(requestType, out var observer))
                //_observers.Add(requestType, [handlerType]);
                _observers.Add(requestType, [(request, context) => [handlerType]] );
            else
                observer.Add((request, context) => [handlerType]);
        }

        /// <summary>
        /// Adds a type to the registry, with a mapping to a single handler. We will add this to any existing handlers for this type
        /// </summary>
        /// <remarks>Mainly intended for internal use, prefer to use <see cref="Register{TRequest}"/></remarks>
        /// <param name="requestType">The <see cref="IRequest"/> type that will be sent/published</param>
        /// <param name="router">The routing function that takes a request and a request context and returns one or more handlers.</param>
        /// <param name="handlerTypes">We need a <see cref="IEnumerable{T}"/> of <see cref="Type"/> that router can return to register with the factory</param>
        public void Add(Type requestType, Func<IRequest?, IRequestContext?, List<Type>> router, IEnumerable<Type> handlerTypes)
        {
            if (!_observers.TryGetValue(requestType, out var observer))
                //_observers.Add(requestType, [handlerType]);
                _observers.Add(requestType, [router] );
            else
                observer.Add(router);
        }

        /// <summary>
        /// Gets this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the t request.</typeparam>
        /// <returns>IEnumerable&lt;Type&gt;.</returns>
        public IEnumerable<Type> Get<TRequest>(TRequest request, IRequestContext requestContext) where TRequest : class, IRequest
        {
            var observerFactories = _observers.TryGetValue(typeof(TRequest), out var factories) ? factories : [];
            var allHandlerTypes = new List<Type>();
            foreach (var observerFactory in observerFactories)
            {
                var handlerTypes = observerFactory(request, requestContext);
                allHandlerTypes.AddRange(handlerTypes);
            }

            return allHandlerTypes;
        }

        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the t request.</typeparam>
        /// <typeparam name="TImplementation">The type of the t implementation.</typeparam>
        public void Register<TRequest, TImplementation>() where TRequest : class, IRequest where TImplementation : class, IHandleRequests<TRequest>
        {
            Add(typeof(TRequest), typeof(TImplementation));
        }

        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the t request.</typeparam>        
        /// <param name="router">The routing function that takes a request and a request context and returns one or more handlers.</param>
        /// <param name="handlerTypes">We need a <see cref="IEnumerable{T}"/> of <see cref="Type"/> that router can return to register with the factory</param>

        public  void Register<TRequest>(Func<IRequest?, IRequestContext?, List<Type>> router, IEnumerable<Type> handlerTypes)
        {
            // Validate that all handlerTypes derive from IHandleRequests
            foreach (var handlerType in handlerTypes)
            {
                if (!typeof(IHandleRequests).IsAssignableFrom(handlerType))
                {
                    throw new ArgumentException(
                        $"Handler type {handlerType.FullName} must derive from IHandleRequests.",
                        nameof(handlerTypes));
                }
            }
            Add(typeof(TRequest), router, handlerTypes);    
        }
        
        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the t request.</typeparam>
        /// <typeparam name="TImplementation">The type of the t implementation.</typeparam>
        public void RegisterAsync<TRequest, TImplementation>() where TRequest : class, IRequest where TImplementation : class, IHandleRequestsAsync<TRequest>
        {
            Add(typeof(TRequest), typeof(TImplementation));
        }

        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the t request.</typeparam>        
        /// <param name="router">The routing function that takes a request and a request context and returns one or more handlers.</param>
        /// <param name="handlerTypes">We need a <see cref="IEnumerable{T}"/> of <see cref="Type"/> that router can return to register with the factory</param>
        public void RegisterAsync<TRequest>(Func<IRequest?, IRequestContext?, List<Type>> router, IEnumerable<Type> handlerTypes)
        {
            // Validate that all handlerTypes derive from IHandleRequestsAsync
            foreach (var handlerType in handlerTypes)
            {
                if (!typeof(IHandleRequestsAsync).IsAssignableFrom(handlerType))
                {
                    throw new ArgumentException(
                        $"Handler type {handlerType.FullName} must derive from IHandleRequestsAsync.",
                        nameof(handlerTypes));
                }
            }
            Add(typeof(TRequest), router, handlerTypes);     
        }
        
        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.</returns>
        public IEnumerator<KeyValuePair<Type, List<Func<IRequest?, IRequestContext?, List<Type>>>>> GetEnumerator()
        {
            return _observers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
