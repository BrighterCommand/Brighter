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
using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// When parsing for message mappers in assemblies, stores any found message mappers. A later step will add these to the message mapper registry
    /// Not used directly
    /// </summary>
    public class ServiceCollectionMessageMapperRegistry: IEnumerable<KeyValuePair<Type, Type>>
    {
        private readonly IServiceCollection _serviceCollection;
        private readonly Dictionary<Type, Type> _mapperCollection = new Dictionary<Type, Type>();
        private readonly ServiceLifetime _lifetime;

        public ServiceCollectionMessageMapperRegistry(IServiceCollection serviceCollection, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        {
            _serviceCollection = serviceCollection;
            _lifetime = lifetime;
        }
        
        /// <summary>
        /// Register a mapper with the collection (generic version)
        /// </summary>
        /// <typeparam name="TRequest">The type of the request to map</typeparam>
        /// <typeparam name="TMessageMapper">The type of the mapper</typeparam>
        public void Register<TRequest, TMessageMapper>() where TRequest : class, IRequest where TMessageMapper : class, IAmAMessageMapper<TRequest>
        {
            Add(typeof(TRequest), typeof(TMessageMapper));
        }
        
        /// <summary>
        /// Add a mapper to the collection
        /// </summary>
        /// <param name="message">The type of message to map</param>
        /// <param name="mapper">The type of the mapper</param>
        public void Add(Type message, Type mapper)
        {
            _serviceCollection.TryAdd(new ServiceDescriptor(mapper, mapper, _lifetime));
            _mapperCollection.Add(message, mapper);
        }

        /// <summary>
        /// Get the genericly typed iterator over this collection
        /// </summary>
        /// <returns>An iterator</returns>
        public IEnumerator<KeyValuePair<Type, Type>> GetEnumerator()
        {
            return _mapperCollection.GetEnumerator();
        }

        /// <summary>
        /// Get the untyped iterator over the collection
        /// </summary>
        /// <returns>An iterator</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
