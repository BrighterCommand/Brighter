#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// The DI-backed <see cref="IAmAScope"/> a container-backed mapper or transformer factory offers a
    /// transform pipeline under <see cref="Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped"/>.
    /// Wraps one <see cref="ServiceProviderLifetimeScope"/> built for this pipeline alone — reusing its
    /// Scoped resolution and caching, but for one pipeline's lifetime rather than the factory's — and
    /// disposes it exactly once under either <see cref="Dispose"/> or <see cref="DisposeAsync"/>.
    /// </summary>
    internal sealed class ServiceProviderPipelineScope(ServiceProviderLifetimeScope lifetimeScope) : IAmAScope
    {
        private int _disposed;

        /// <summary>
        /// The per-pipeline lifetime scope every participating factory resolves the pipeline's Scoped
        /// artefacts through.
        /// </summary>
        internal ServiceProviderLifetimeScope LifetimeScope { get; } = lifetimeScope;

        /// <summary>
        /// Resolves <paramref name="objectType"/> through <see cref="LifetimeScope"/>, discarding the
        /// release token. Used by a container-backed factory with nothing to release eagerly (the
        /// handler family, whose scope is released as a whole when the pipeline completes).
        /// </summary>
        /// <exception cref="ConfigurationException">
        /// <see cref="LifetimeScope"/> is borrowed over an ambient whose owner disposed it while this
        /// resolution was in flight. See the <see cref="Create{T}(Type,out object)"/> overload.
        /// </exception>
        internal T? Create<T>(Type objectType) where T : class => Create<T>(objectType, out _);

        /// <summary>
        /// Resolves <paramref name="objectType"/> through <see cref="LifetimeScope"/>, the one path every
        /// container-backed mapper/transformer/handler factory shares to reach a possibly-borrowed
        /// ambient. When <see cref="LifetimeScope"/> is borrowed, the ambient's owner - not Brighter -
        /// disposes its underlying scope, and can do so after this pipeline already adopted it but before
        /// (or during) a later resolution; that race is not one a re-probe can close (see
        /// <c>AmbientScopeProbe</c>), so the resulting <see cref="ObjectDisposedException"/> is translated
        /// here, at the single site every caller reaches, into a <see cref="ConfigurationException"/>
        /// naming the ambient's own provider - a genuine fault, not a declined adoption. The owned path
        /// (this scope's own <see cref="IServiceScope"/>) never disposes itself mid-resolution, so nothing
        /// is translated there.
        /// </summary>
        /// <param name="objectType">The concrete type to create</param>
        /// <param name="releaseToken">The resolution's own release token; see
        /// <see cref="ServiceProviderLifetimeScope.GetOrCreate{T}(Type,out object)"/>.</param>
        /// <exception cref="ConfigurationException">
        /// <see cref="LifetimeScope"/> is borrowed and its ambient was disposed by its owner while this
        /// pipeline was resolving from it.
        /// </exception>
        internal T? Create<T>(Type objectType, out object? releaseToken) where T : class
        {
            try
            {
                return LifetimeScope.GetOrCreate<T>(objectType, out releaseToken);
            }
            catch (ObjectDisposedException e) when (LifetimeScope.IsBorrowed)
            {
                throw new ConfigurationException(
                    $"The ambient offered by '{LifetimeScope.AmbientProviderType?.Name}' was disposed while a " +
                    "pipeline was resolving from it. Give each owner its own ambient scope, or do not dispose " +
                    "an ambient until every pipeline resolving from it has completed.",
                    e);
            }
        }

        /// <summary>
        /// Disposes the pipeline's DI scope, releasing everything resolved through it. Disposes through
        /// <see cref="ServiceProviderLifetimeScope.DisposeSurfacing"/> so a disposal failure reaches the
        /// caller rather than being logged and swallowed as factory-teardown would.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            LifetimeScope.DisposeSurfacing();
        }

        /// <summary>
        /// Disposes the pipeline's DI scope asynchronously, through
        /// <see cref="ServiceProviderLifetimeScope.DisposeSurfacingAsync"/> for the same reason as
        /// <see cref="Dispose"/>.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return default;
            return LifetimeScope.DisposeSurfacingAsync();
        }
    }
}
