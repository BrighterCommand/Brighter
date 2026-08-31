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

namespace Paramore.Brighter
{
    /// <summary>
    /// A handle to the DI scope a transform pipeline, or a handler pipeline, takes for its lifetime.
    /// </summary>
    /// <remarks>
    /// One <see cref="IAmAScope"/> is created per pipeline by whichever participating factory can offer
    /// one, and disposed when the pipeline is released. Every mapper and transform in the pipeline
    /// resolves from the same scope, so a container-registered <c>Scoped</c> dependency shared by a
    /// mapper and its transforms is one instance for that pipeline. A handler pipeline's own handle is
    /// carried on its <see cref="IAmALifetime"/> instead, so every handler and decorator it resolves
    /// shares it in the same way.
    /// <para>
    /// Distinct from <see cref="IAmALifetime"/>, which tracks the handler instances a handler pipeline
    /// has created; a lifetime scope holds a handle, it does not become one.
    /// </para>
    /// </remarks>
    public interface IAmAScope : IDisposable, IAsyncDisposable
    {
    }
}
