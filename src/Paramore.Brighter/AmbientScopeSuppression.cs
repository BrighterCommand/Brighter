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

namespace Paramore.Brighter
{
    /// <summary>
    /// Carries a single bit - whether ambient scope adoption is suppressed on the current logical
    /// flow - so that a <c>Publish</c>'s per-subscriber isolation can propagate into pipelines nested
    /// inside a subscriber.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This member is <b>not intended for direct application use</b>. It is public because
    /// <c>Paramore.Brighter.ServiceActivator</c> - a container package honouring FR-8 under NFR-7 -
    /// and Brighter's own tests all live in assemblies separate from <c>Paramore.Brighter</c>, and this
    /// repository uses no <c>InternalsVisibleTo</c>.
    /// </para>
    /// <para>
    /// Backed by an <see cref="AsyncLocal{T}"/>, so a bracket taken on one logical flow is visible to
    /// everything that flow calls or awaits, but not to a flow that already branched away from it
    /// (for example, a <see cref="System.Threading.Tasks.Task"/> already started on another thread).
    /// </para>
    /// <para>
    /// <see cref="Suppress"/> returns an idempotent bracket - disposing it more than once is a no-op -
    /// but it has two misuse modes neither this type nor its bracket detects: disposing a bracket on a
    /// logical flow other than the one that took it, and disposing brackets out of order. Neither is
    /// reachable from Brighter's own three lexical brackets.
    /// </para>
    /// </remarks>
    public static class AmbientScopeSuppression
    {
        private static readonly AsyncLocal<bool> Flag = new();

        /// <summary>
        /// Whether ambient scope adoption is suppressed on the current logical flow.
        /// </summary>
        public static bool IsSuppressed => Flag.Value;

        /// <summary>
        /// Suppresses ambient scope adoption on the current logical flow until the returned bracket is
        /// disposed, which restores whatever value was in effect when this was called.
        /// </summary>
        /// <returns>A bracket that restores the captured value on <see cref="IDisposable.Dispose"/>.
        /// Disposing it more than once is a no-op.</returns>
        public static IDisposable Suppress()
        {
            var captured = Flag.Value;
            Flag.Value = true;
            return new SuppressionScope(captured);
        }

        private sealed class SuppressionScope(bool captured) : IDisposable
        {
            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    Flag.Value = captured;
                }
            }
        }
    }
}
