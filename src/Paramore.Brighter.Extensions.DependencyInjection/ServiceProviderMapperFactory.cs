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
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// Creates a message mapper from the underlying .NET IoC container.
    /// Supports singleton, scoped, and transient lifetimes based on <see cref="IBrighterOptions.MapperLifetime"/>.
    /// </summary>
    public class ServiceProviderMapperFactory : IAmAMessageMapperFactory, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ServiceProviderLifetimeScope _lifetimeScope;
        private readonly IAmAScopeProvider? _scopeProvider;
        private readonly ScopeAffinityPolicy _scopeAffinityPolicy;

        /// <summary>
        /// Constructs a mapper factory that uses the .NET Service Provider for implementation details
        /// </summary>
        /// <param name="serviceProvider">The .NET IoC container</param>
        public ServiceProviderMapperFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            var options = (IBrighterOptions?)serviceProvider.GetService(typeof(IBrighterOptions));
            var lifetime = options?.MapperLifetime ?? ServiceLifetime.Singleton;
            _lifetimeScope = new ServiceProviderLifetimeScope(serviceProvider, lifetime);
            _scopeProvider = (IAmAScopeProvider?)serviceProvider.GetService(typeof(IAmAScopeProvider));
            _scopeAffinityPolicy = new ScopeAffinityPolicy(options);
        }

        /// <summary>
        /// Offers a pipeline scope when this factory's configured lifetime is <c>Scoped</c>, so the
        /// pipeline's mapper (and, once offered by the transformer factory too, its transforms) resolve
        /// from one DI scope per pipeline rather than a factory-wide one. Any other lifetime offers none
        /// and asks nothing - only a <c>Scoped</c> pipeline ever asks for an ambient.
        /// </summary>
        /// <exception cref="AmbientScopeSourceException">
        /// A registered <see cref="IAmAScopeProvider"/>'s <c>GetAmbient</c> threw. The calling pipeline
        /// builder recognises this type and rethrows the inner exception unwrapped.
        /// </exception>
        public IAmAScope? CreatePipelineScope()
        {
            if (_lifetimeScope.Lifetime != ServiceLifetime.Scoped) return null;

            AmbientScopeQuery.Ask(_scopeProvider, _scopeAffinityPolicy.ForTransformPipeline());
            return new ServiceProviderPipelineScope(new ServiceProviderLifetimeScope(_serviceProvider, ServiceLifetime.Scoped));
        }

        /// <summary>
        /// Create an instance of the message mapper type from the .NET IoC container.
        /// Lifetime is determined by <see cref="IBrighterOptions.MapperLifetime"/>.
        /// </summary>
        /// <param name="messageMapperType">The type of mapper to instantiate</param>
        /// <param name="scope">
        /// The pipeline scope this factory offered via <see cref="CreatePipelineScope"/>. Resolved through
        /// when supplied and this factory's lifetime is <c>Scoped</c>; otherwise resolution falls back to
        /// this factory's own lifetime scope.
        /// </param>
        /// <returns>The created mapper instance</returns>
        public Lease<IAmAMessageMapper>? Create(Type messageMapperType, IAmAScope? scope = null)
        {
            if (scope is ServiceProviderPipelineScope pipelineScope && _lifetimeScope.Lifetime == ServiceLifetime.Scoped)
            {
                var scopedMapper = pipelineScope.LifetimeScope.GetOrCreate<IAmAMessageMapper>(messageMapperType, out var scopedReleaseToken);
                return scopedMapper is null ? null : new Lease<IAmAMessageMapper>(scopedMapper, scopedReleaseToken);
            }

            if (_lifetimeScope.Lifetime == ServiceLifetime.Scoped)
            {
                var freshMapper = _lifetimeScope.GetOrCreateIsolated<IAmAMessageMapper>(messageMapperType, out var freshReleaseToken);
                return freshMapper is null ? null : new Lease<IAmAMessageMapper>(freshMapper, freshReleaseToken);
            }

            var mapper = _lifetimeScope.GetOrCreate<IAmAMessageMapper>(messageMapperType, out var releaseToken);
            return mapper is null ? null : new Lease<IAmAMessageMapper>(mapper, releaseToken);
        }

        /// <summary>
        /// Releases a mapper created by this factory, disposing the per-instance
        /// <see cref="Microsoft.Extensions.DependencyInjection.IServiceScope"/> a transient mapper was
        /// resolved from. Without this the scope — and any <see cref="IDisposable"/> mapper it holds —
        /// is retained until the factory is disposed at shutdown.
        /// </summary>
        /// <param name="lease">The lease returned by <see cref="Create"/> for the mapper to release</param>
        public void Release(Lease<IAmAMessageMapper>? lease)
        {
            //over-release of a lease is a harmless no-op, including a null lease
            if (lease is null) return;
            _lifetimeScope.Release(lease.ReleaseToken);
        }

        /// <summary>
        /// Disposes of the factory and its lifetime scope.
        /// </summary>
        public void Dispose()
        {
            _lifetimeScope.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
