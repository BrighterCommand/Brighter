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
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// Holds the <c>Scoped</c> artefacts one DI scope has produced, keyed by type, so that identity
    /// follows the scope rather than the pipeline handle built over it.
    /// </summary>
    /// <remarks>
    /// Registered <c>TryAddScoped</c>. <see cref="ServiceProviderLifetimeScope"/>'s <c>Scoped</c> path
    /// resolves this from whichever scope is in play instead of owning a private dictionary of its own:
    /// from an ambient's own <c>Services</c> when borrowed - one instance per request scope, shared by
    /// every pipeline in that request - and from the <see cref="Microsoft.Extensions.DependencyInjection.IServiceScope"/>
    /// it just created when owned - one instance per pipeline, today's behaviour. That is what gives a
    /// borrowed scope's artefacts request-wide identity (two <c>Post</c>s in one request share one
    /// mapper) while an owned scope's artefacts keep per-pipeline identity.
    /// </remarks>
    public sealed class ScopedArtefactCache : IDisposable
    {
        private readonly ConcurrentDictionary<Type, Lazy<object?>> _cache = new();

        /// <summary>
        /// Returns the single instance of <paramref name="type"/> this cache holds, resolving it
        /// through <paramref name="factory"/> on first ask. Concurrent first-resolvers of one type
        /// produce one instance; the losers see the winner's. A resolution that throws is not
        /// remembered: it is evicted before the exception is rethrown, so a later ask for the same
        /// type tries <paramref name="factory"/> again instead of replaying the same failure forever.
        /// </summary>
        /// <param name="type">The artefact type to resolve.</param>
        /// <param name="factory">Resolves one instance of <paramref name="type"/>.</param>
        /// <returns>The single instance of <paramref name="type"/> held by this cache.</returns>
        public object? GetOrAdd(Type type, Func<object?> factory)
        {
            var lazy = _cache.GetOrAdd(type, _ => new Lazy<object?>(factory));
            try
            {
                return lazy.Value;
            }
            catch
            {
                //remove only this call's own (now faulted) Lazy, and only if it is still the entry
                //at type - a concurrent resolver may already have evicted it and published a fresh,
                //healthy Lazy in its place, which this must not delete
                ((ICollection<KeyValuePair<Type, Lazy<object?>>>)_cache).Remove(
                    new KeyValuePair<Type, Lazy<object?>>(type, lazy));
                throw;
            }
        }

        /// <summary>
        /// Drops this cache's references. Disposes no artefact it holds: the .NET container already
        /// tracks a disposable resolution against the scope that created it, and disposes it when that
        /// scope closes.
        /// </summary>
        public void Dispose() => _cache.Clear();
    }
}
