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
