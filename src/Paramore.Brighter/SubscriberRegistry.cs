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
using System.Linq;

namespace Paramore.Brighter
{
    public record KeyedType(string Key, Type Type);
    
    /// <summary>
    /// Class SubscriberRegistry.
    /// In order to map an <see cref="IHandleRequests"/> or an <see cref="IHandleRequestsAsync"/> 
    /// to a <see cref="Command"/> or an <see cref="Event"/> we need you to register 
    /// the association via the <see cref="SubscriberRegistry"/>
    /// The default implementation of <see cref="SubscriberRegistry"/> is usable in most instances 
    /// and this is provided for testing
    /// </summary>
    public class SubscriberRegistry : IAmASubscriberRegistry, IAmAnAsyncSubcriberRegistry,
        IEnumerable<KeyValuePair<Type, List<Type>>>
    {
        private readonly Dictionary<Type, List<KeyedType>> _observers = new Dictionary<Type, List<KeyedType>>();

        /// <summary>
        /// Gets this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <returns>IEnumerable&lt;Type&gt;.</returns>
        public IEnumerable<Type> Get<TRequest>() where TRequest : class, IRequest
        {
            var observed = _observers.ContainsKey(typeof(TRequest));
            return observed ? _observers[typeof(TRequest)].Select(kt => kt.Type) : new List<Type>();
        }

        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <typeparam name="THandleRequests">The type of the handler.</typeparam>
        public void Register<TRequest, THandleRequests>() where TRequest : class, IRequest where THandleRequests : class, IHandleRequests<TRequest>
        {
            Add(typeof(TRequest), typeof(THandleRequests));
        }

        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <param name="contextBagKey">The key to use to retrieve this handler</param>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <typeparam name="THandleRequests">The type of the handler.</typeparam>
        public void Register<TRequest, THandleRequests>(string contextBagKey) where TRequest : class, IRequest where THandleRequests : class, IHandleRequests<TRequest>
        {
            Add(typeof(TRequest), typeof(THandleRequests));
        }

        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <typeparam name="THandleRequestsAsync">The type of the handler.</typeparam>
        public void RegisterAsync<TRequest, THandleRequestsAsync>() where TRequest : class, IRequest where THandleRequestsAsync : class, IHandleRequestsAsync<TRequest>
        {
            Add(typeof(TRequest), typeof(THandleRequestsAsync));
        }
        
        /// <summary>
        /// Registers a request type and handler type
        /// </summary>
        /// <param name="requestType">The type of the request</param>
        /// <param name="handlerType">The typd of the handler</param>
        public void Add(Type requestType, Type handlerType)
        {
            var observed = _observers.ContainsKey(requestType);
            if (!observed)
                _observers.Add(requestType, new List<KeyedType> { new KeyedType(null, handlerType) });
            else
                _observers[requestType].Add(new KeyedType(null, handlerType));
        }

        /// <summary>
        /// Registers a request type and handler type against a key
        /// </summary>
        /// <param name="key">The key used to find the request and handler type</param>
        /// <param name="requestType">The type of the request</param>
        /// <param name="handlerType">The typd of the handler</param>
        public void Add(string key, Type requestType, Type handlerType)
        {
            var observed = _observers.ContainsKey(requestType);
            if (!observed)
                _observers.Add(requestType, new List<KeyedType> {new KeyedType(null, handlerType) });
            else
                _observers[requestType].Add(new KeyedType(null, handlerType));
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.</returns>
        public IEnumerator<KeyValuePair<Type, List<Type>>> GetEnumerator()
        {
            return _observers.Select(kv => new KeyValuePair<Type, List<Type>>(kv.Key, kv.Value.Select(kt => kt.Type).ToList()))
                .GetEnumerator()  ;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }


    }
}
