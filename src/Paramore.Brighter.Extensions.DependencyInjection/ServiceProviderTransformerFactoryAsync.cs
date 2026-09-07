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
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// A factory for creating async transformers, backed by the .NET Service Collection.
    /// Supports singleton, scoped, and transient lifetimes based on <see cref="IBrighterOptions.TransformerLifetime"/>.
    /// </summary>
    public class ServiceProviderTransformerFactoryAsync : IAmAMessageTransformerFactoryAsync, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ServiceProviderLifetimeScope _lifetimeScope;
        private readonly IAmAScopeProvider? _scopeProvider;
        private readonly ScopeAffinityPolicy _scopeAffinityPolicy;

        /// <summary>
        /// Constructs a transformer factory
        /// </summary>
        /// <param name="serviceProvider">The IoC container we use to satisfy requests for transforms</param>
        public ServiceProviderTransformerFactoryAsync(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            var options = (IBrighterOptions?)serviceProvider.GetService(typeof(IBrighterOptions));
            var lifetime = options?.TransformerLifetime ?? ServiceLifetime.Singleton;
            _lifetimeScope = new ServiceProviderLifetimeScope(serviceProvider, lifetime);
            _scopeProvider = (IAmAScopeProvider?)serviceProvider.GetService(typeof(IAmAScopeProvider));
            _scopeAffinityPolicy = new ScopeAffinityPolicy(options);
        }

        /// <summary>
        /// Offers a pipeline scope when this factory's configured lifetime is <c>Scoped</c>, so the
        /// pipeline's transforms (and, once offered by the mapper factory too, its mapper) resolve from
        /// one DI scope per pipeline rather than a factory-wide one. Any other lifetime offers none and
        /// asks nothing - only a <c>Scoped</c> pipeline ever asks for an ambient.
        /// </summary>
        /// <exception cref="AmbientScopeSourceException">
        /// A registered <see cref="IAmAScopeProvider"/>'s <c>GetAmbient</c> threw. The calling pipeline
        /// builder recognises this type and rethrows the inner exception unwrapped.
        /// </exception>
        public IAmAScope? CreatePipelineScope()
        {
            if (_lifetimeScope.Lifetime != ServiceLifetime.Scoped) return null;

            var borrowed = AmbientScopeQuery.Ask(_scopeProvider, _scopeAffinityPolicy.ForTransformPipeline(), _serviceProvider);
            return borrowed ?? new ServiceProviderPipelineScope(new ServiceProviderLifetimeScope(_serviceProvider, ServiceLifetime.Scoped));
        }

        /// <summary>
        /// Creates a specific transformer on demand.
        /// Lifetime is determined by <see cref="IBrighterOptions.TransformerLifetime"/>.
        /// </summary>
        /// <param name="transformerType">The type of transformer to create</param>
        /// <param name="scope">
        /// The pipeline scope this factory offered via <see cref="CreatePipelineScope"/>. Resolved through
        /// when supplied and this factory's lifetime is <c>Scoped</c>; otherwise resolution falls back to
        /// this factory's own lifetime scope.
        /// </param>
        /// <returns>The created transformer instance</returns>
        public Lease<IAmAMessageTransformAsync>? Create(Type transformerType, IAmAScope? scope = null)
        {
            if (scope is ServiceProviderPipelineScope pipelineScope && _lifetimeScope.Lifetime == ServiceLifetime.Scoped)
            {
                var scopedTransform = pipelineScope.LifetimeScope.GetOrCreate<IAmAMessageTransformAsync>(transformerType, out var scopedReleaseToken);
                return scopedTransform is null ? null : new Lease<IAmAMessageTransformAsync>(scopedTransform, scopedReleaseToken);
            }

            if (_lifetimeScope.Lifetime == ServiceLifetime.Scoped)
            {
                var freshTransform = _lifetimeScope.GetOrCreateIsolated<IAmAMessageTransformAsync>(transformerType, out var freshReleaseToken);
                return freshTransform is null ? null : new Lease<IAmAMessageTransformAsync>(freshTransform, freshReleaseToken);
            }

            var transform = _lifetimeScope.GetOrCreate<IAmAMessageTransformAsync>(transformerType, out var releaseToken);
            return transform is null ? null : new Lease<IAmAMessageTransformAsync>(transform, releaseToken);
        }

        /// <summary>
        /// Releases a transformer. For singleton lifetime, does nothing.
        /// For scoped/transient, disposes the per-resolution scope the transformer was resolved from.
        /// </summary>
        /// <param name="lease">The lease returned by <see cref="Create"/> for the transformer to release</param>
        public void Release(Lease<IAmAMessageTransformAsync>? lease)
        {
            //over-release of a lease is a harmless no-op, including a null lease
            if (lease is null) return;
            _lifetimeScope.Release(lease.ReleaseToken);
        }

        /// <summary>
        /// Releases a transformer asynchronously, awaiting disposal of the per-instance
        /// <see cref="IServiceScope"/> a transient transformer was resolved from. Preferred over
        /// <see cref="Release"/> on the Proactor pump thread: awaiting an <see cref="IAsyncDisposable"/>
        /// transform's disposal does not block the single-threaded synchronization context.
        /// </summary>
        /// <param name="lease">The lease returned by <see cref="Create"/> for the transformer to release</param>
        public ValueTask ReleaseAsync(Lease<IAmAMessageTransformAsync>? lease)
        {
            if (lease is null) return default;
            return _lifetimeScope.ReleaseAsync(lease.ReleaseToken);
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
