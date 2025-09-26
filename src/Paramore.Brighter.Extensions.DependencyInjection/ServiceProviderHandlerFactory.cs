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
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// A factory for handlers using the .NET IoC container for implementation details
    /// </summary>
    public class ServiceProviderHandlerFactory : IAmAHandlerFactorySync, IAmAHandlerFactoryAsync
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly bool _isSingleton;
        private readonly ConcurrentDictionary<IAmALifetime, IServiceScope> _scopes = new();

        /// <summary>
        /// Constructs a factory that uses the .NET IoC container as the factory
        /// </summary>
        /// <param name="serviceProvider">The .NET IoC container</param>
        public ServiceProviderHandlerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            var options = (IBrighterOptions?) serviceProvider.GetService(typeof(IBrighterOptions));
            if (options == null) _isSingleton = false; else _isSingleton = options.HandlerLifetime == ServiceLifetime.Singleton;
        }

        /// <summary>
        /// Creates an instance of the request handler
        /// Lifetime is set during registration
        /// </summary>
        /// <param name="handlerType">The type of handler to request</param>
        /// <param name="lifetime">The brighter Handler lifetime</param>
        /// <returns>An instantiated request handler</returns>
        IHandleRequests? IAmAHandlerFactorySync.Create(Type handlerType, IAmALifetime lifetime)
        {
            if (_isSingleton)
                return (IHandleRequests?)_serviceProvider.GetService(handlerType);

            if (!_scopes.ContainsKey(lifetime))
                _scopes.TryAdd(lifetime, _serviceProvider.CreateScope());
            
            return (IHandleRequests?)_scopes[lifetime].ServiceProvider.GetService(handlerType);
        }

        /// <summary>
        /// Creates an instance of the request handler
        /// Lifetime is set during registration
        /// </summary>
        /// <param name="handlerType">The type of handler to request</param>
        /// <param name="lifetime">The brighter Handler lifetime</param>
        /// <returns>An instantiated request handler</returns>
        IHandleRequestsAsync? IAmAHandlerFactoryAsync.Create(Type handlerType, IAmALifetime lifetime)
        {
            if (_isSingleton)
                return (IHandleRequestsAsync?)_serviceProvider.GetService(handlerType);
            
            if (!_scopes.ContainsKey(lifetime))
                _scopes.TryAdd(lifetime, _serviceProvider.CreateScope());
            
            return (IHandleRequestsAsync?)_scopes[lifetime].ServiceProvider.GetService(handlerType);
        }

        /// <summary>
        /// Release the request handler - actual behavior depends on lifetime, we only dispose if we are transient
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="lifetime">The brighter Handler lifetime</param>
        public void Release(IHandleRequests handler, IAmALifetime lifetime)
        {
            if (_isSingleton) return;
            
            var disposal = handler as IDisposable;
            disposal?.Dispose();
            
            ReleaseScope(lifetime);
        }

        public void Release(IHandleRequestsAsync? handler, IAmALifetime lifetime)
        {
            if (_isSingleton) return;
            
            var disposal = handler as IDisposable;
            disposal?.Dispose();
            ReleaseScope(lifetime);
        }

        private void ReleaseScope(IAmALifetime lifetime)
        {
            if(!_scopes.TryGetValue(lifetime, out IServiceScope? scope))
                return;
            
            scope.Dispose();
            _scopes.TryRemove(lifetime, out _);
        }
    }
}
