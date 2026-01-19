#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// Helper class for ServiceProvider-backed factories that provides consistent lifetime handling
    /// for singleton, scoped, and transient object creation. This class extracts the common
    /// lifetime management pattern used across handler, mapper, and transformer factories.
    /// </summary>
    internal sealed class ServiceProviderLifetimeScope : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ServiceLifetime _lifetime;
        private readonly ConcurrentDictionary<Type, Lazy<object?>> _singletonInstances = new();
        private readonly ConcurrentDictionary<Type, Lazy<object?>> _scopedInstances = new();
        private IServiceScope? _scope;
        private bool _disposed;

        /// <summary>
        /// Constructs a lifetime scope helper
        /// </summary>
        /// <param name="serviceProvider">The .NET IoC container</param>
        /// <param name="lifetime">The lifetime for created objects</param>
        public ServiceProviderLifetimeScope(IServiceProvider serviceProvider, ServiceLifetime lifetime)
        {
            _serviceProvider = serviceProvider;
            _lifetime = lifetime;
        }

        /// <summary>
        /// Gets the configured lifetime for objects created by this scope
        /// </summary>
        public ServiceLifetime Lifetime => _lifetime;

        /// <summary>
        /// Creates or retrieves an object of the specified type according to the configured lifetime.
        /// - Singleton: Returns the same instance for all calls with the same type
        /// - Scoped: Returns the same instance for all calls with the same type within this scope
        /// - Transient: Creates a new instance for each call (from a scoped provider for proper disposal)
        /// </summary>
        /// <typeparam name="T">The interface type to cast the result to</typeparam>
        /// <param name="objectType">The concrete type to create</param>
        /// <returns>The created or cached instance, or null if not registered</returns>
        public T? GetOrCreate<T>(Type objectType) where T : class
        {
            return _lifetime switch
            {
                ServiceLifetime.Singleton => GetOrCreateSingleton<T>(objectType),
                ServiceLifetime.Scoped => GetOrCreateScoped<T>(objectType),
                ServiceLifetime.Transient => GetTransient<T>(objectType),
                _ => throw new InvalidOperationException($"Unsupported lifetime: {_lifetime}")
            };
        }

        /// <summary>
        /// Gets or creates a singleton instance. Thread-safe using Lazy&lt;T&gt;.
        /// Singletons are shared across all calls for the same type.
        /// </summary>
        private T? GetOrCreateSingleton<T>(Type objectType) where T : class
        {
            var lazy = _singletonInstances.GetOrAdd(objectType, _ =>
                new Lazy<object?>(() => _serviceProvider.GetService(objectType)));
            return (T?)lazy.Value;
        }

        /// <summary>
        /// Gets or creates a scoped instance. Thread-safe using Lazy&lt;T&gt;.
        /// Scoped instances are shared within this scope.
        /// </summary>
        private T? GetOrCreateScoped<T>(Type objectType) where T : class
        {
            var lazy = _scopedInstances.GetOrAdd(objectType, _ =>
                new Lazy<object?>(() => GetTransient<T>(objectType)));
            return (T?)lazy.Value;
        }

        /// <summary>
        /// Creates a transient instance from a scoped service provider.
        /// Using a scope ensures proper disposal of transient instances.
        /// </summary>
        private T? GetTransient<T>(Type objectType) where T : class
        {
            _scope ??= _serviceProvider.CreateScope();
            return (T?)_scope.ServiceProvider.GetService(objectType);
        }

        /// <summary>
        /// Releases an object. For singleton lifetime, does nothing as singletons
        /// are managed by the container. For scoped/transient, disposes if IDisposable.
        /// </summary>
        /// <param name="instance">The object to release</param>
        public void Release(object? instance)
        {
            if (_lifetime == ServiceLifetime.Singleton) return;

            if (instance is IDisposable disposal)
                disposal.Dispose();
        }

        /// <summary>
        /// Disposes of the scope, cleaning up any service scopes and cached instances.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _scope?.Dispose();
            _scopedInstances.Clear();
            // Note: Don't clear singleton instances as they may be shared

            _disposed = true;
        }
    }
}
