// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-10-2014
// ***********************************************************************
// <copyright file="Interpreter.cs" company="">
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
using paramore.brighter.commandprocessor.extensions;

/// <summary>
/// The commandprocessor namespace.{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
/// </summary>
namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Class Interpreter.{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
    /// </summary>
    /// <typeparam name="TRequest">The type of the t request.</typeparam>
    internal class Interpreter<TRequest> where TRequest : class, IRequest
    {
        private readonly IAmASubscriberRegistry registry;
        private readonly IAmAHandlerFactory handlerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="Interpreter{TRequest}"/> class.
        /// </summary>
        /// <param name="registry">The registry.</param>
        /// <param name="handlerFactory">The handler factory.</param>
        public Interpreter(IAmASubscriberRegistry registry, IAmAHandlerFactory handlerFactory)
        {
            this.registry = registry ;
            this.handlerFactory = handlerFactory;
        }

        /// <summary>
        /// Gets the handlers.
        /// </summary>
        /// <param name="requestType">Type of the request.</param>
        /// <returns>IEnumerable&lt;RequestHandler&lt;TRequest&gt;&gt;.</returns>
        public IEnumerable<RequestHandler<TRequest>> GetHandlers(Type requestType)
        {
            return new RequestHandlers<TRequest>(registry.Get<TRequest>().Select(handlerType => handlerFactory.Create(handlerType)).Cast<IHandleRequests<TRequest>>());
        }
    }
}