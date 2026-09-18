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
    /// An <see cref="IAmAScope"/> that names the <see cref="IServiceProvider"/> behind it, so a
    /// container-backed Brighter factory can resolve a pipeline's artefacts and their dependencies
    /// from it. Implement this on an ambient scope offered by an <see cref="IAmAScopeProvider"/>.
    /// The implementer owns the underlying scope; Brighter never disposes it.
    /// </summary>
    /// <remarks>
    /// A role interface, not a base class - any assembly can implement it without depending on this
    /// package's other types. It lives here rather than in core because it names
    /// <see cref="IServiceProvider"/>, which core must not reference. A Microsoft-container-backed
    /// factory type-tests for this interface, never for a concrete class:
    /// <c>if (ambient is IAmAServiceProviderScope src)</c>. An ambient offered by a provider that does
    /// not implement it is ignored, not rejected - the factory declines and creates its own scope.
    /// </remarks>
    public interface IAmAServiceProviderScope : IAmAScope
    {
        /// <summary>
        /// The provider a pipeline adopting this ambient resolves from. Must not throw, must not be
        /// <see langword="null"/>, and must not be the container's root provider - an ambient is a DI
        /// scope the caller owns, and the root is not one. May name a provider whose scope has already
        /// been disposed; Brighter probes for that before resolving anything through it.
        /// </summary>
        IServiceProvider Services { get; }
    }
}
