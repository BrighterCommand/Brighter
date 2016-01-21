// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-10-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

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
using System.Collections.Generic;
using System.Linq;

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Class Interpreter
    /// The <see cref="Interpreter{T}"/> is the dispatcher element of the Command Dispatcher. It looks up the <see cref="IRequest"/> in the <see cref="SubscriberRegistry"/>
    /// to find registered <see cref="IHandleRequests"/> and returns to the PipelineBuilder, which in turn will call the client provided <see cref="IAmAHandlerFactory"/>
    /// to create instances of the the handlers.
    /// </summary>
    /// <typeparam name="TRequest">The type of the t request.</typeparam>
    internal class Interpreter<TRequest> where TRequest : class, IRequest
    {
        private readonly IAmASubscriberRegistry _registry;
        private readonly IAmAHandlerFactory _handlerFactory;
        private readonly IAmAnAsyncHandlerFactory _asyncHandlerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="Interpreter{TRequest}"/> class.
        /// </summary>
        /// <param name="registry">The registry.</param>
        /// <param name="handlerFactory">The handler factory.</param>
        internal Interpreter(IAmASubscriberRegistry registry, IAmAHandlerFactory handlerFactory)
            : this(registry, handlerFactory, null)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Interpreter{TRequest}"/> class.
        /// </summary>
        /// <param name="registry">The registry.</param>
        /// <param name="asyncHandlerFactory">The async handler factory.</param>
        internal Interpreter(IAmASubscriberRegistry registry, IAmAnAsyncHandlerFactory asyncHandlerFactory)
            : this(registry, null, asyncHandlerFactory)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Interpreter{TRequest}"/> class.
        /// </summary>
        /// <param name="registry">The registry.</param>
        /// <param name="handlerFactory">The handler factory.</param>
        /// <param name="asyncHandlerFactory">The async handler factory.</param>
        internal Interpreter(IAmASubscriberRegistry registry, IAmAHandlerFactory handlerFactory, IAmAnAsyncHandlerFactory asyncHandlerFactory)
        {
            _registry = registry;
            _handlerFactory = handlerFactory;
            _asyncHandlerFactory = asyncHandlerFactory;
        }

        /// <summary>
        /// Gets the handlers.
        /// </summary>
        /// <param name="requestType">Type of the request.</param>
        /// <returns>IEnumerable&lt;RequestHandler&lt;TRequest&gt;&gt;.</returns>
        internal IEnumerable<RequestHandler<TRequest>> GetHandlers(Type requestType)
        {
            return new RequestHandlers<TRequest>(
                _registry.Get<TRequest>()
                    .Select(handlerType => _handlerFactory.Create(handlerType))
                    .Cast<IHandleRequests<TRequest>>());
        }

        /// <summary>
        /// Gets the async handlers.
        /// </summary>
        /// <param name="requestType">Type of the request.</param>
        /// <returns><see cref="IEnumerable{AsyncRequestHandler}"/>.</returns>
        internal IEnumerable<RequestHandlerAsync<TRequest>> GetAsyncHandlers(Type requestType)
        {
            return new AsyncRequestHandlers<TRequest>(
                _registry.Get<TRequest>()
                    .Select(handlerType => _asyncHandlerFactory.Create(handlerType))
                    .Cast<IHandleRequestsAsync<TRequest>>());
        }
    }
}
