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

using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// Names the <see cref="IBrighterOptions"/> descriptor <c>RegisterBrighterOptions</c> added to the
    /// service collection, so a validator can ask whether the last unkeyed <see cref="IBrighterOptions"/>
    /// descriptor is the one Brighter registered - without resolving anything.
    /// </summary>
    /// <remarks>
    /// Carries no affinity and no options object - only the identity of the descriptor. Identity is
    /// reference equality against the <see cref="ServiceDescriptor"/> instance <c>services.Add</c>
    /// received, which survives a snapshot that copies the descriptor list rather than the descriptors
    /// themselves.
    /// </remarks>
    internal sealed class BrighterOptionsRegistration(ServiceDescriptor descriptor)
    {
        /// <summary>
        /// The <see cref="IBrighterOptions"/> descriptor this registration names.
        /// </summary>
        public ServiceDescriptor Descriptor { get; } = descriptor;
    }
}
