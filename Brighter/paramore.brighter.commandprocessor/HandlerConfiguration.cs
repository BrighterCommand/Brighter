// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-02-2014
//
// Last Modified By : ian
// Last Modified On : 07-10-2014
// ***********************************************************************
// <copyright file="HandlerConfiguration.cs" company="">
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

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Class HandlerConfiguration
    /// </summary>
    public class HandlerConfiguration
    {
        /// <summary>
        /// Gets the subscriber registry.
        /// </summary>
        /// <value>The subscriber registry.</value>
        public IAmASubscriberRegistry SubscriberRegistry { get; private set; }
        /// <summary>
        /// Gets the handler factory.
        /// </summary>
        /// <value>The handler factory.</value>
        public IAmAHandlerFactory HandlerFactory { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HandlerConfiguration"/> class.
        /// We use the <see cref="IAmASubscriberRegistry"/> instance to look up subscribers for messages when dispatching. Use <see cref="SubscriberRegistry"/> unless
        /// you have some reason to override. We expect a <see cref="CommandProcessor.Send{T}(T)"/> to have one registered handler
        /// We use the 
        /// </summary>
        /// <param name="subscriberRegistry">The subscriber registry.</param>
        /// <param name="handlerFactory">The handler factory.</param>
        public HandlerConfiguration(IAmASubscriberRegistry subscriberRegistry, IAmAHandlerFactory handlerFactory)
        {
            SubscriberRegistry = subscriberRegistry;
            HandlerFactory = handlerFactory;
        }
    }
}
