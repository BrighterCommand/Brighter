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
    /// <summary>
    /// Class MessageMapperRegistry
    /// In order to use a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> approach we require you to provide
    /// a <see cref="IAmAMessageMapper"/> to map between <see cref="Command"/> or <see cref="Event"/> and a <see cref="Message"/> 
    /// registered via <see cref="IAmAMessageMapperRegistry"/>
    /// This is a default implementation of<see cref="IAmAMessageMapperRegistry"/> which is suitable for most usages, the interface is provided mainly for testing
    /// </summary>
    public class MessageMapperRegistry : IAmAMessageMapperRegistry, IAmAMessageMapperRegistryAsync 
    {
        private readonly IAmAMessageMapperFactory _messageMapperFactory;
        private readonly IAmAMessageMapperFactoryAsync _messageMapperFactoryAsync;
        private readonly Dictionary<Type, Type> _messageMappers = new Dictionary<Type, Type>();
        private readonly Dictionary<Type, Type> _asyncMessageMappers = new Dictionary<Type, Type>();

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageMapperRegistry"/> class.
        /// </summary>
        /// <param name="messageMapperFactory">The message mapper factory.</param>
        /// <param name="messageMapperFactoryAsync">The async message mapper factory</param>
        public MessageMapperRegistry(IAmAMessageMapperFactory messageMapperFactory, IAmAMessageMapperFactoryAsync messageMapperFactoryAsync)
        {
            _messageMapperFactory = messageMapperFactory;
            _messageMapperFactoryAsync = messageMapperFactoryAsync;

            if (messageMapperFactory == null && messageMapperFactoryAsync == null)
                throw new ConfigurationException("Should have at least one factory");
        }

        /// <summary>
        /// Gets this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the t request.</typeparam>
        /// <returns>IAmAMessageMapper&lt;TRequest&gt;.</returns>
        public IAmAMessageMapper<TRequest> Get<TRequest>() where TRequest : class, IRequest
        {
            if (_messageMappers.ContainsKey(typeof(TRequest)))
            {
                var messageMapperType = _messageMappers[typeof(TRequest)];
                return (IAmAMessageMapper<TRequest>)_messageMapperFactory.Create(messageMapperType);
            }
            else
            {
                return null;
            }
        }
        
        /// <summary>
        /// Gets this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the t request.</typeparam>
        /// <returns>IAmAMessageMapperAsync&lt;TRequest&gt;.</returns>
        public IAmAMessageMapperAsync<TRequest> GetAsync<TRequest>() where TRequest : class, IRequest
        {
            if (_asyncMessageMappers.ContainsKey(typeof(TRequest)))
            {
                var messageMapperType = _asyncMessageMappers[typeof(TRequest)];
                return (IAmAMessageMapperAsync<TRequest>)_messageMapperFactoryAsync.Create(messageMapperType);
            }
            else
            {
                return null;
            }
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

            _messageMappers.Add(typeof(TRequest), typeof(TMessageMapper));
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

            _messageMappers.Add(request, mapper);
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

            _asyncMessageMappers.Add(typeof(TRequest), typeof(TMessageMapper));

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

            _asyncMessageMappers.Add(request, mapper);
        }
    }
}
