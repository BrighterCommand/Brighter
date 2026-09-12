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

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// The scope affinity selected by an opt-in registration extension. Registering one of these is
    /// how a package that knows nothing about Brighter's registration paths sets the default affinity
    /// on whichever options object <see cref="IBrighterOptions"/> resolves to, in any registration
    /// order. It wins over any affinity the application assigned itself (D18).
    /// </summary>
    /// <remarks>
    /// Register it as a constructed instance under a plain <c>AddSingleton</c> —
    /// <c>services.AddSingleton(new ScopeAffinityOverride(affinity))</c>. Never <c>TryAdd*</c>, which
    /// would make the first call win the affinity while the last call wins the provider, and never a
    /// factory delegate, whose descriptor carries no instance for validation to read an affinity from.
    /// </remarks>
    public sealed class ScopeAffinityOverride
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeAffinityOverride"/> class.
        /// </summary>
        /// <param name="affinity">The affinity this opt-in gesture selects.</param>
        public ScopeAffinityOverride(ScopeAffinity affinity) => Affinity = affinity;

        /// <summary>
        /// The affinity this opt-in gesture selected.
        /// </summary>
        public ScopeAffinity Affinity { get; }
    }
}
