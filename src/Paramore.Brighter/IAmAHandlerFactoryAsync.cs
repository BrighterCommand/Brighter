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
    /// Interface IAmAnAysyncHandlerFactory
    /// We do not know how to create instances of <see cref="IHandleRequestsAsync"/> implemented by your application, but need to create instances to instantiate a pipeline.
    /// To achieve this we require clients of the Paramore.Brighter.CommandProcessor library need to implement <see cref="IAmAHandlerFactoryAsync"/> to provide 
    /// instances of their <see cref="IHandleRequestsAsync"/> types. You need to provide a Handler Factory to support all <see cref="IHandleRequestsAsync"/> registered 
    /// with <see cref="IAmASubscriberRegistry"/>. Typically you would use an IoC container to implement the Handler Factory.
    /// </summary>
    public interface IAmAHandlerFactoryAsync
    {
        /// <summary>
        /// Creates the specified async handler type.
        /// </summary>
        /// <param name="handlerType">Type of the handler.</param>
        /// <returns>IHandleRequestsAsync.</returns>
        IHandleRequestsAsync Create(Type handlerType);
        /// <summary>
        /// Releases the specified async handler.
        /// </summary>
        /// <param name="handler">The handler.</param>
        void Release(IHandleRequestsAsync handler);
    }
}
