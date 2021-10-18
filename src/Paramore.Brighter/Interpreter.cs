﻿#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter
{
    internal class Interpreter<TRequest> where TRequest : class, IRequest
    {
        private readonly IAmASubscriberRegistry _registry;
        private readonly IAmAHandlerFactorySync _handlerFactorySync;
        private readonly IAmAHandlerFactoryAsync _asyncHandlerFactory;

        internal Interpreter(IAmASubscriberRegistry registry, IAmAHandlerFactorySync handlerFactorySync)
            : this(registry, handlerFactorySync, null)
        { }

        internal Interpreter(IAmASubscriberRegistry registry, IAmAHandlerFactoryAsync asyncHandlerFactory)
            : this(registry, null, asyncHandlerFactory)
        { }

        internal Interpreter(IAmASubscriberRegistry registry, IAmAHandlerFactorySync handlerFactorySync, IAmAHandlerFactoryAsync asyncHandlerFactory)
        {
            _registry = registry;
            _handlerFactorySync = handlerFactorySync;
            _asyncHandlerFactory = asyncHandlerFactory;
        }

        internal IEnumerable<RequestHandler<TRequest>> GetHandlers(IAmALifetime lifetimeScope)
        {
            return new RequestHandlers<TRequest>(
                _registry.Get<TRequest>()
                    .Select(handlerType => _handlerFactorySync.Create(handlerType, lifetimeScope))
                    .Cast<IHandleRequests<TRequest>>());
        }

        internal IEnumerable<RequestHandlerAsync<TRequest>> GetAsyncHandlers(IAmALifetime lifetimeScope)
        {
            return new AsyncRequestHandlers<TRequest>(
                _registry.Get<TRequest>()
                    .Select(handlerType => _asyncHandlerFactory.Create(handlerType, lifetimeScope))
                    .Cast<IHandleRequestsAsync<TRequest>>());
        }
    }
}
