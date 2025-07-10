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
using System.Collections.Concurrent;
using System.Collections.Generic;
using Paramore.Brighter.MessageMappers;

namespace Paramore.Brighter
{
    /// <summary>
    /// Class MessageMapperRegistry
    /// In order to use a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> approach we require you to provide
    /// a <see cref="IAmAMessageMapper"/> to map between <see cref="Command"/> or <see cref="Event"/> and a <see cref="Message"/> 
    /// registered via <see cref="IAmAMessageMapperRegistry"/>
    /// This is a default implementation of<see cref="IAmAMessageMapperRegistry"/> which is suitable for most usages, the interface is provided mainly for testing
    /// </summary>
    public class MessageMapperRegistry : IAmAMessageMapperRegistry, IAmAMessageMapperRegistryAsync 
    {
        private readonly IAmAMessageMapperFactory? _messageMapperFactory;
        private readonly IAmAMessageMapperFactoryAsync? _messageMapperFactoryAsync;
        private readonly ConcurrentDictionary<Type, Type> _messageMappers = new();
        private readonly ConcurrentDictionary<Type, Type> _asyncMessageMappers = new();
        private readonly Type? _defaultMessageMapper;
        private readonly Type? _defaultMessageMapperAsync;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageMapperRegistry"/> class.
        /// </summary>
        /// <param name="messageMapperFactory">The message mapper factory.</param>
        /// <param name="messageMapperFactoryAsync">The async message mapper factory</param>
        /// <param name="defaultMessageMapper">The default message Mapper</param>
        /// <param name="defaultMessageMapperAsync">The default Async Message Mapper</param>
        public MessageMapperRegistry(IAmAMessageMapperFactory? messageMapperFactory,
            IAmAMessageMapperFactoryAsync? messageMapperFactoryAsync, Type? defaultMessageMapper = null,
            Type? defaultMessageMapperAsync = null)
        {
            _messageMapperFactory = messageMapperFactory;
            _messageMapperFactoryAsync = messageMapperFactoryAsync;
            _defaultMessageMapper = defaultMessageMapper;
            _defaultMessageMapperAsync = defaultMessageMapperAsync;

            if (messageMapperFactory == null && messageMapperFactoryAsync == null)
                throw new ConfigurationException("Should have at least one factory");
        }

        /// <summary>
        /// Gets this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the t request.</typeparam>
        /// <returns>IAmAMessageMapper&lt;TRequest&gt;.</returns>
        public IAmAMessageMapper<TRequest>? Get<TRequest>() where TRequest : class, IRequest
        {
            if (_messageMapperFactory is null)
                return null;

            if (!_messageMappers.TryGetValue(typeof(TRequest), out var messageMapperType) && _defaultMessageMapper != null)
            {
                messageMapperType = _defaultMessageMapper.MakeGenericType(typeof(TRequest));
                _messageMappers.TryAdd(typeof(TRequest), messageMapperType);
            }

            if (messageMapperType is null)
                return null;

            return (IAmAMessageMapper<TRequest>?)_messageMapperFactory.Create(messageMapperType);
        }

        /// <summary>
        /// Gets this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the t request.</typeparam>
        /// <returns>IAmAMessageMapperAsync&lt;TRequest&gt;.</returns>
        public IAmAMessageMapperAsync<TRequest>? GetAsync<TRequest>() where TRequest : class, IRequest
        {
            if (_messageMapperFactoryAsync is null)
                return null;
            
            if (!_asyncMessageMappers.TryGetValue(typeof(TRequest), out var messageMapperType) && _defaultMessageMapperAsync != null)
            {
                messageMapperType = _defaultMessageMapperAsync.MakeGenericType(typeof(TRequest));
                _asyncMessageMappers.TryAdd(typeof(TRequest), messageMapperType);
            }

            if (messageMapperType is null)
                return null;

            return (IAmAMessageMapperAsync<TRequest>?)_messageMapperFactoryAsync.Create(messageMapperType);
        }

        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the t request.</typeparam>
        /// <typeparam name="TMessageMapper">The type of the t message mapper.</typeparam>
        /// <exception cref="System.ArgumentException"></exception>
        public void Register<TRequest, TMessageMapper>() where TRequest : class, IRequest where TMessageMapper : class, IAmAMessageMapper<TRequest>
        {
            if (_messageMappers.ContainsKey(typeof(TRequest)))
                throw new ArgumentException(string.Format("Message type {0} already has a mapper; only one mapper can be registered per type", typeof(TRequest).Name));

            _messageMappers.TryAdd(typeof(TRequest), typeof(TMessageMapper));
        }

        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <param name="request">The type of the request to map</param>
        /// <param name="mapper">The type of the mapper for this request</param>
        /// <exception cref="System.ArgumentException"></exception>
        public void Register(Type request, Type mapper)
        {
            if (_messageMappers.ContainsKey(request))
                throw new ArgumentException(string.Format("Message type {0} already has a mapper; only one mapper can be registered per type", request.Name));

            _messageMappers.TryAdd(request, mapper);
        }
        
        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the t request.</typeparam>
        /// <typeparam name="TMessageMapper">The type of the t message mapper.</typeparam>
        /// <exception cref="System.ArgumentException"></exception>
        public void RegisterAsync<TRequest, TMessageMapper>() where TRequest : class, IRequest where TMessageMapper : class, IAmAMessageMapperAsync<TRequest>
        {
            if (_asyncMessageMappers.ContainsKey(typeof(TRequest)))
                throw new ArgumentException(string.Format("Message type {0} already has a mapper; only one mapper can be registered per type", typeof(TRequest).Name));

            _asyncMessageMappers.TryAdd(typeof(TRequest), typeof(TMessageMapper));

        }
        
        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <param name="request">The type of the request to map</param>
        /// <param name="mapper">The type of the mapper for this request</param>
        /// <exception cref="System.ArgumentException"></exception>
        public void RegisterAsync(Type request, Type mapper)
        {
            if (_asyncMessageMappers.ContainsKey(request))
                throw new ArgumentException(string.Format("Message type {0} already has a mapper; only one mapper can be registered per type", request.Name));

            _asyncMessageMappers.TryAdd(request, mapper);
        }
    }
}
