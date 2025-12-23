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
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// A factory for handlers using the .NET IoC container for implementation details
    /// </summary>
    public class ServiceProviderHandlerFactory : IAmAHandlerFactorySync, IAmAHandlerFactoryAsync
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ServiceLifetime _handlerLifetime;
        private readonly ConcurrentDictionary<IAmALifetime, IServiceScope> _scopes = new();
        private readonly ConcurrentDictionary<(IAmALifetime, Type), object> _scopedInstances = new();

        /// <summary>
        /// Constructs a factory that uses the .NET IoC container as the factory
        /// </summary>
        /// <param name="serviceProvider">The .NET IoC container</param>
        public ServiceProviderHandlerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            var options = (IBrighterOptions?) serviceProvider.GetService(typeof(IBrighterOptions));
            _handlerLifetime = options?.HandlerLifetime ?? ServiceLifetime.Transient;
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
            return _handlerLifetime switch
            {
                ServiceLifetime.Singleton => (IHandleRequests?)_serviceProvider.GetService(handlerType),
                ServiceLifetime.Scoped => GetOrCreateScoped<IHandleRequests>(handlerType, lifetime),
                ServiceLifetime.Transient => GetTransient<IHandleRequests>(handlerType, lifetime),
                _ => throw new InvalidOperationException($"Unsupported handler lifetime: {_handlerLifetime}")
            };
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
            return _handlerLifetime switch
            {
                ServiceLifetime.Singleton => (IHandleRequestsAsync?)_serviceProvider.GetService(handlerType),
                ServiceLifetime.Scoped => GetOrCreateScoped<IHandleRequestsAsync>(handlerType, lifetime),
                ServiceLifetime.Transient => GetTransient<IHandleRequestsAsync>(handlerType, lifetime),
                _ => throw new InvalidOperationException($"Unsupported handler lifetime: {_handlerLifetime}")
            };
        }

        /// <summary>
        /// Release the request handler - actual behavior depends on lifetime, we only dispose if we are transient
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="lifetime">The brighter Handler lifetime</param>
        public void Release(IHandleRequests handler, IAmALifetime lifetime)
        {
            if (_handlerLifetime == ServiceLifetime.Singleton) return;

            var disposal = handler as IDisposable;
            disposal?.Dispose();

            ReleaseScope(lifetime);
        }

        public void Release(IHandleRequestsAsync? handler, IAmALifetime lifetime)
        {
            if (_handlerLifetime == ServiceLifetime.Singleton) return;

            var disposal = handler as IDisposable;
            disposal?.Dispose();
            ReleaseScope(lifetime);
        }

        private T? GetOrCreateScoped<T>(Type handlerType, IAmALifetime lifetime) where T : class
        {
            var key = (lifetime, handlerType);

            if (_scopedInstances.TryGetValue(key, out var cached))
                return (T?)cached;

            var instance = GetTransient<T>(handlerType, lifetime);
            if (instance != null)
                _scopedInstances.TryAdd(key, instance);

            return instance;
        }

        private T? GetTransient<T>(Type handlerType, IAmALifetime lifetime) where T : class
        {
            if (!_scopes.ContainsKey(lifetime))
                _scopes.TryAdd(lifetime, _serviceProvider.CreateScope());

            return (T?)_scopes[lifetime].ServiceProvider.GetService(handlerType);
        }

        private void ReleaseScope(IAmALifetime lifetime)
        {
            if(!_scopes.TryGetValue(lifetime, out IServiceScope? scope))
                return;

            // Clear scoped instances for this lifetime
            var keysToRemove = _scopedInstances.Keys.Where(k => k.Item1 == lifetime).ToList();
            foreach (var key in keysToRemove)
                _scopedInstances.TryRemove(key, out _);

            scope.Dispose();
            _scopes.TryRemove(lifetime, out _);
        }
    }
}
