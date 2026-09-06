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

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// The ambient-source ask shared by every container-backed factory's <c>CreatePipelineScope()</c>.
    /// </summary>
    /// <remarks>
    /// T4.3 replaces this unconditional ask with the affinity computation and the canonical ladder in
    /// front of it (ADR 0072 steps 2, 3), by way of <c>ScopeAffinityPolicy</c>. Until then, every
    /// factory asks whenever a provider is registered, regardless of its own configured lifetime.
    /// </remarks>
    internal static class AmbientScopeQuery
    {
        /// <summary>
        /// Asks <paramref name="scopeProvider"/> for an ambient, when one is registered, discarding
        /// what it returns - nothing yet adopts it.
        /// </summary>
        /// <exception cref="AmbientScopeSourceException">
        /// <paramref name="scopeProvider"/>'s <c>GetAmbient</c> threw. The calling pipeline builder
        /// recognises this type and rethrows the inner exception unwrapped.
        /// </exception>
        public static void Ask(IAmAScopeProvider? scopeProvider)
        {
            if (scopeProvider is null) return;

            try
            {
                scopeProvider.GetAmbient(ScopeAffinity.AlwaysNew);
            }
            catch (Exception e)
            {
                throw new AmbientScopeSourceException(e);
            }
        }
    }
}
