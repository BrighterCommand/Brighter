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
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// A factory for handlers using the .NET IoC container for implementation details
    /// </summary>
    public class ServiceProviderHandlerFactory : IAmAHandlerFactorySync, IAmAHandlerFactoryAsync
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly bool _isTransient;

        /// <summary>
        /// Constructs a factory that uses the .NET IoC container as the factory
        /// </summary>
        /// <param name="serviceProvider">The .NET IoC container</param>
        public ServiceProviderHandlerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            var options = (IBrighterOptions) serviceProvider.GetService(typeof(IBrighterOptions));
            if (options == null) _isTransient = true; else _isTransient = options.HandlerLifetime == ServiceLifetime.Transient;
        }

        /// <summary>
        /// Creates an instance of the request handler
        /// Lifetime is set during registration
        /// </summary>
        /// <param name="handlerType">The type of handler to request</param>
        /// <returns>An instantiated request handler</returns>
        IHandleRequests IAmAHandlerFactorySync.Create(Type handlerType)
        {
            return (IHandleRequests)_serviceProvider.GetService(handlerType);
        }

        /// <summary>
        /// Creates an instance of the request handler
        /// Lifetime is set during registration
        /// </summary>
        /// <param name="handlerType">The type of handler to request</param>
        /// <returns>An instantiated request handler</returns>
        IHandleRequestsAsync IAmAHandlerFactoryAsync.Create(Type handlerType)
        {
            return (IHandleRequestsAsync)_serviceProvider.GetService(handlerType);
        }

        /// <summary>
        /// Release the request handler - actual behavior depends on lifetime, we only dispose if we are transient
        /// </summary>
        /// <param name="handler"></param>
        public void Release(IHandleRequests handler)
        {
            if (!_isTransient) return;

            var disposal = handler as IDisposable;
            disposal?.Dispose();
        }

        public void Release(IHandleRequestsAsync handler)
        {
            if (!_isTransient) return;

            var disposal = handler as IDisposable;
            disposal?.Dispose();
        }
    }
}
