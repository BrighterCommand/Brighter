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
    /// Carries an exception thrown by an <see cref="IAmAScopeProvider"/>'s <c>GetAmbient</c> out of a
    /// pipeline builder, so it can be recognised and rethrown unwrapped instead of being folded into a
    /// <see cref="ConfigurationException"/> like any other build failure.
    /// </summary>
    /// <remarks>
    /// The one type in this seam that an implementer outside Brighter - including a container-backed
    /// factory in a third-party package - is obliged to construct: any factory that asks an
    /// <see cref="IAmAScopeProvider"/> for an ambient must wrap a throw from that ask in this type.
    /// <see cref="Exception.InnerException"/> is never <see langword="null"/> on a constructed instance,
    /// which is what licenses an unconditional dereference at the call sites that unwrap it.
    /// </remarks>
    public class AmbientScopeSourceException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AmbientScopeSourceException"/> class.
        /// </summary>
        /// <param name="inner">The exception the ambient source threw.</param>
        public AmbientScopeSourceException(Exception inner)
            : base(inner?.Message, inner)
        {
        }
    }
}
