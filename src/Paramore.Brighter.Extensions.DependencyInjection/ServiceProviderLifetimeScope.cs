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
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        private readonly ConcurrentDictionary<object, IServiceScope> _transientScopes =
            new(InstanceComparer.Default);
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
        /// Scoped instances are shared within this scope and disposed when the scope is disposed.
        /// </summary>
        private T? GetOrCreateScoped<T>(Type objectType) where T : class
        {
            _scope ??= _serviceProvider.CreateScope();
            var lazy = _scopedInstances.GetOrAdd(objectType, _ =>
                new Lazy<object?>(() => (T?)_scope.ServiceProvider.GetService(objectType)));
            return (T?)lazy.Value;
        }

        /// <summary>
        /// Creates a transient instance in its own short-lived <see cref="IServiceScope"/>.
        /// The scope is tracked by instance only when the instance implements <see cref="IDisposable"/>;
        /// in that case <see cref="Release"/> must be called to dispose the scope and prevent unbounded
        /// memory growth. For non-disposable instances the scope is disposed immediately so that no
        /// reference accumulates between calls.
        /// </summary>
        private T? GetTransient<T>(Type objectType) where T : class
        {
            var scope = _serviceProvider.CreateScope();
            var instance = (T?)scope.ServiceProvider.GetService(objectType);
            if (instance is IDisposable)
                _transientScopes[instance] = scope;
            else
                scope.Dispose();
            return instance;
        }

        /// <summary>
        /// Releases an object back to the scope that owns it.
        /// <para>
        /// Only a transient instance has a scope of its own to drain: disposing that scope makes the
        /// DI container drop its reference and dispose the instance exactly once. A singleton is owned
        /// by the container, and a scoped instance is owned by <c>_scope</c> and stays cached in
        /// <c>_scopedInstances</c> for reuse — disposing either here would hand out a disposed instance
        /// on the next <see cref="GetOrCreate{T}"/>, so both are a no-op.
        /// </para>
        /// <para>
        /// Releasing the same instance twice, or releasing an instance that was never tracked (a
        /// non-disposable transient), is a safe no-op — nothing is disposed more than once.
        /// </para>
        /// </summary>
        /// <param name="instance">The object to release</param>
        public void Release(object? instance)
        {
            if (_lifetime != ServiceLifetime.Transient) return;

            if (instance != null && _transientScopes.TryRemove(instance, out var scope))
                scope.Dispose();
        }

        /// <summary>
        /// Disposes of the scope, cleaning up any service scopes and cached instances.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            foreach (var scope in _transientScopes.Values)
                scope.Dispose();
            _transientScopes.Clear();

            _scope?.Dispose();
            _scopedInstances.Clear();
            // Note: Don't clear singleton instances as they may be shared

            _disposed = true;
        }

        private sealed class InstanceComparer : IEqualityComparer<object>
        {
            internal static readonly InstanceComparer Default = new();

            bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);

            int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
