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
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// Reports the three ways an ambient scope adoption ask can fail to adopt, each latched once
    /// per (<see cref="Condition"/>, provider implementation type) per Brighter container (D19).
    /// </summary>
    /// <remarks>
    /// Registered <c>TryAddSingleton</c> in <c>ServiceCollectionExtensions.BrighterHandlerBuilder</c>,
    /// scoped to the host's root service provider rather than the process, so a test process building
    /// several hosts latches each afresh (FR-18, FR-23, FR-24.2, FR-24.4). Nothing calls
    /// <see cref="WarnOnce"/> yet - the five container-backed factories that ask it arrive in later
    /// tasks, and until then this type is inert. <see cref="WarnOnce"/> itself is a stub here: it logs
    /// on every call rather than the once-per-pair latch its final contract requires, since the latch
    /// is behaviour with nothing yet to exercise it.
    /// </remarks>
    internal sealed class AmbientScopeDiagnostics
    {
        private readonly ILogger<AmbientScopeDiagnostics> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AmbientScopeDiagnostics"/> class.
        /// </summary>
        /// <param name="logger">The logger this Brighter container's diagnostics are written to.</param>
        public AmbientScopeDiagnostics(ILogger<AmbientScopeDiagnostics> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// The three ways an ambient scope adoption ask can fail to adopt (FR-23, FR-24.2, FR-24.4).
        /// </summary>
        public enum Condition
        {
            /// <summary>A <c>JoinAmbient</c> ask returned nothing (FR-24.2). Never raised for an
            /// <c>AlwaysNew</c> ask, which is FR-10-conforming behaviour when it returns nothing.</summary>
            NoAmbientOffered,

            /// <summary>A <c>JoinAmbient</c> ask returned an ambient that does not implement this
            /// package's hand-off role, or one that failed the usability probe (FR-23).</summary>
            AmbientUnusable,

            /// <summary>An <c>AlwaysNew</c> ask returned an ambient, contrary to FR-10. Brighter
            /// ignores it and creates and owns the pipeline scope as usual (FR-24.4).</summary>
            AmbientIgnoredForAlwaysNew
        }

        /// <summary>
        /// Logs <paramref name="condition"/> at <see cref="LogLevel.Warning"/>, naming
        /// <paramref name="providerImplementationType"/>.
        /// </summary>
        /// <param name="condition">The condition that occurred.</param>
        /// <param name="providerImplementationType">The implementation type of the
        /// <see cref="IAmAScopeProvider"/> that was asked.</param>
        public void WarnOnce(Condition condition, Type providerImplementationType)
        {
            _logger.LogWarning(
                "Brighter ambient scope diagnostic {Condition} for provider {ProviderType}",
                condition,
                providerImplementationType);
        }
    }
}
