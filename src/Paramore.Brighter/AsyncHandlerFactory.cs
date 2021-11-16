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

namespace Paramore.Brighter
{
    /// <summary>
    /// Class AsyncHandlerFactory
    /// </summary>
    /// <typeparam name="TRequest">The type of the t request.</typeparam>
    internal static class AsyncHandlerFactory
    {
        /// <summary>
        /// Creates the async request handler.
        /// </summary>
        /// <param name="factory">The async handler factory.</param>
        /// <param name="attribute">The attribute.</param>
        /// <param name="requestContext">The request context.</param>
        /// <returns><see cref="IHandleRequestsAsync{TRequest}"/>.</returns>
        public static IHandleRequestsAsync<TRequest> CreateAsyncRequestHandler<TRequest>(this IAmAHandlerFactoryAsync factory, RequestHandlerAttribute attribute, IRequestContext requestContext)
             where TRequest : class, IRequest
        {
            var handlerType = attribute.GetHandlerType().MakeGenericType(typeof(TRequest));
            var handler = (IHandleRequestsAsync<TRequest>)factory.Create(handlerType);

            if (handler is null)
                throw new ConfigurationException($"Could not create handler {handlerType} from {factory}"); 
            //Load the context before the initializer - in case we want to use the context from within the initializer
            handler.Context = requestContext;
            handler.InitializeFromAttributeParams(attribute.InitializerParams());
            return handler;
        }
    }
}
