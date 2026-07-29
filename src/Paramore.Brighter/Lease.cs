#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter
{
    /// <summary>
    /// A handle returned by a mapper/transformer factory's <c>Create</c> (or the registry's <c>Get</c>) that
    /// identifies a <b>single resolution</b> of an <paramref name="Instance"/> so it can later be released back
    /// to the factory that produced it.
    /// </summary>
    /// <remarks>
    /// A lease pairs the resolved <see cref="Instance"/> with an opaque <see cref="ReleaseToken"/>. The token
    /// keys the release on the <em>resolution</em>, not on the instance's identity. This matters when a factory
    /// hands out a <b>shared</b> instance — e.g. a container-<c>Singleton</c> mapper/transform resolved under the
    /// default <c>Transient</c> <c>MapperLifetime</c>/<c>TransformerLifetime</c>: every resolution has its own
    /// scope to release, but they all share one instance, so releasing by instance identity cannot tell the
    /// resolutions apart and can tear down a scope another live resolution still depends on. Keying on the lease
    /// closes that whole class of bug: the factory releases exactly the one scope this lease represents, and an
    /// over-release of a lease is a harmless no-op rather than a pop of another resolution's scope.
    /// <para>
    /// The <see cref="ReleaseToken"/> is deliberately typed as <see cref="object"/>: the concrete handle
    /// (for the DI-backed factories, the resolution's <c>IServiceScope</c>) lives in another assembly, so a
    /// caller can carry the token but not interpret it. A factory that hands out a shared instance or holds no
    /// per-resolution state leaves the token <c>null</c> and makes release a no-op.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The type of the resolved instance the lease holds.</typeparam>
    public sealed class Lease<T> where T : class
    {
        /// <summary>
        /// Creates a lease over a resolved instance.
        /// </summary>
        /// <param name="instance">The resolved instance.</param>
        /// <param name="releaseToken">
        /// An opaque handle the producing factory recognises to release exactly this resolution; <c>null</c>
        /// for a factory that holds no per-resolution state (a shared instance, or a no-op factory).
        /// </param>
        public Lease(T instance, object? releaseToken = null)
        {
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
            ReleaseToken = releaseToken;
        }

        /// <summary>
        /// The resolved instance this lease holds.
        /// </summary>
        public T Instance { get; }

        /// <summary>
        /// An opaque handle to the resolution, recognised only by the factory that produced this lease. Carry
        /// it from create to release; do not interpret it.
        /// </summary>
        public object? ReleaseToken { get; }

        /// <summary>
        /// Wraps a bare instance in a lease with no release token — an <b>untracked</b>, release-is-a-no-op
        /// lease, for callers that hold no per-resolution state to reclaim: a factory that hands out a shared
        /// instance, a no-op factory, or a test constructing a pipeline directly. The name describes the
        /// token-lessness, not the instance — the point is that <c>Release</c> reclaims nothing, whether or not
        /// the wrapped instance happens to be shared. Naming the case keeps "nothing is reclaimed on release" a visible,
        /// deliberate act: a factory that opens a per-resolution scope <b>must</b> return a lease carrying its
        /// release token (via the constructor), and a bare instance can no longer become a token-less lease by
        /// an implicit conversion that would make every <c>Release</c> a silent no-op.
        /// </summary>
        /// <param name="instance">The instance to wrap.</param>
        public static Lease<T> Untracked(T instance) => new(instance);
    }
}
