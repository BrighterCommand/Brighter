// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : Fred
// Created          : 2015-12-21
//                    Based on HandlerFactory
//
// Last Modified By : Fred
// Last Modified On : 2015-12-21
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

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Class AsyncHandlerFactory
    /// </summary>
    /// <typeparam name="TRequest">The type of the t request.</typeparam>
    internal class AsyncHandlerFactory<TRequest> where TRequest : class, IRequest
    {
        private readonly RequestHandlerAttribute _attribute;
        private readonly IAmAnAsyncHandlerFactory _factory;
        private readonly Type _messageType;
        private readonly IRequestContext _requestContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncHandlerFactory{TRequest}"/> class.
        /// </summary>
        /// <param name="attribute">The attribute.</param>
        /// <param name="factory">The async handler factory.</param>
        /// <param name="requestContext">The request context.</param>
        public AsyncHandlerFactory(RequestHandlerAttribute attribute, IAmAnAsyncHandlerFactory factory, IRequestContext requestContext)
        {
            _attribute = attribute;
            _factory = factory;
            _requestContext = requestContext;
            _messageType = typeof(TRequest);
        }

        /// <summary>
        /// Creates the async request handler.
        /// </summary>
        /// <returns><see cref="IHandleRequestsAsync{TRequest}"/>.</returns>
        public IHandleRequestsAsync<TRequest> CreateAsyncRequestHandler()
        {
            var handlerType = _attribute.GetHandlerType().MakeGenericType(_messageType);
            var handler = (IHandleRequestsAsync<TRequest>)_factory.Create(handlerType);
            //Lod the context before the initializer - in case we want to use the context from within the initializer
            handler.Context = _requestContext;
            handler.InitializeFromAttributeParams(_attribute.InitializerParams());
            return handler;
        }
    }
}
