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
using Paramore.Brighter.MessageMappers;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
   /// </summary>
    /// <remarks>
    /// When parsing for message mappers in assemblies, stores any found message mappers. A later step will add these to the message mapper registry
    /// Not used directly
    /// This builder registers the open generic type <see cref="CloudEventJsonMessageMapper{TRequest}"/> as the default mapper.
    /// When Brighter needs to map a message and no explicit mapper has been registered for the message's type,
    /// it will instantiate a closed generic version of <see cref="CloudEventJsonMessageMapper{TRequest}"/> with the specific request type.
    /// This ensures that messages are serialized and deserialized according to the CloudEvents JSON format.
    /// You can override this behaviour by providing a different default message mapper.
    /// </remarks>
    public class ServiceCollectionMessageMapperRegistryBuilder(IServiceCollection serviceCollection, ServiceLifetime lifetime = ServiceLifetime.Singleton) 
    {
        public Dictionary<Type, Type> Mappers { get; } = new Dictionary<Type, Type>();
        public Type DefaultMessageMapper { get; private set; } = typeof(JsonMessageMapper<>);
        public Dictionary<Type, Type> AsyncMappers { get; } = new Dictionary<Type, Type>();
        public Type DefaultMessageMapperAsync { get; private set; } = typeof(JsonMessageMapper<>);

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
        /// Register a mapper with the collection (generic version)
        /// </summary>
        /// <typeparam name="TRequest">The type of the request to map</typeparam>
        /// <typeparam name="TMessageMapper">The type of the mapper</typeparam>
        public void RegisterAsync<TRequest, TMessageMapper>() where TRequest : class, IRequest where TMessageMapper : class, IAmAMessageMapperAsync<TRequest>
        {
            AddAsync(typeof(TRequest), typeof(TMessageMapper));
        }
        
        /// <summary>
        /// Add a mapper to the collection
        /// </summary>
        /// <param name="message">The type of message to map</param>
        /// <param name="mapper">The type of the mapper</param>
        public void Add(Type message, Type mapper)
        {
            serviceCollection.TryAdd(new ServiceDescriptor(mapper, mapper, lifetime));
            Mappers.Add(message, mapper);
        }

        /// <summary>
        /// Add a mapper to the collection
        /// </summary>
        /// <param name="message">The type of message to map</param>
        /// <param name="mapper">The type of the mapper</param>
        public void AddAsync(Type message, Type mapper)
        {
            serviceCollection.TryAdd(new ServiceDescriptor(mapper, mapper, lifetime));
            AsyncMappers.Add(message, mapper);
        }
 
        /// <summary>
        /// Ensure that the default mappers are registered with Dependency Injection
        /// </summary>
        public void EnsureDefaultMessageMapperIsRegistered()
        {
                serviceCollection.TryAdd(new ServiceDescriptor(DefaultMessageMapper, DefaultMessageMapper, lifetime));
                serviceCollection.TryAdd(new ServiceDescriptor(DefaultMessageMapperAsync, DefaultMessageMapperAsync, lifetime));
        }
        
        
        /// <summary>
        /// Set the Default Message Mapper
        /// </summary>
        /// <param name="defaultMapper">Type of default Message Mapper</param>
        public void SetDefaultMessageMapper(Type defaultMapper)
        {
            serviceCollection.TryAdd(new ServiceDescriptor(defaultMapper, defaultMapper, lifetime));
            DefaultMessageMapper = defaultMapper;
        }
        
        /// <summary>
        /// Set the Default Async Message Mapper
        /// </summary>
        /// <param name="defaultMapper">Type of default async message mapper</param>
        public void SetDefaultMessageMapperAsync(Type defaultMapper)
        {
            serviceCollection.TryAdd(new ServiceDescriptor(defaultMapper, defaultMapper, lifetime));
            DefaultMessageMapperAsync = defaultMapper;
        }    
    }
}
