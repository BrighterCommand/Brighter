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
    /// The subset of a <see cref="Microsoft.Extensions.DependencyInjection.ServiceDescriptor"/> the
    /// scope-configuration rules can read without resolving anything (ADR 0074 step 2). One shape serves
    /// three different populations - the ambient scope-provider registrations, the affinity-override
    /// registrations and the <see cref="IBrighterOptions"/> registrations - because the rules that read
    /// each ask the same questions of it.
    /// </summary>
    /// <remarks>
    /// <paramref name="IsBrighterRegistered"/> is meaningful only on a <see cref="DescriptorRecord"/> built
    /// for the <see cref="IBrighterOptions"/> population - it names whether this descriptor is the one
    /// <c>RegisterBrighterOptions</c> added, as <see cref="BrighterOptionsRegistration"/> records it. It is
    /// always <see langword="false"/> on a record built for either of the other two populations.
    /// </remarks>
    /// <param name="Position">This descriptor's index within the registration-ordered list it was read
    /// from. The only identity a descriptor with neither a statically known implementation type nor an
    /// instance can offer.</param>
    /// <param name="ServiceKey">The descriptor's service key, or <see langword="null"/> when it is
    /// unkeyed.</param>
    /// <param name="ImplementationType">The implementation type, where one is statically known - read
    /// from <c>KeyedImplementationType</c> on a keyed descriptor and <c>ImplementationType</c> otherwise.
    /// <see langword="null"/> when the descriptor was registered by factory delegate or by instance.</param>
    /// <param name="ImplementationInstance">The instance the descriptor supplies, where it supplies one;
    /// otherwise <see langword="null"/>.</param>
    /// <param name="IsBrighterRegistered">On an <see cref="IBrighterOptions"/> entry, whether Brighter's
    /// own registration produced this descriptor. Not meaningful, and always <see langword="false"/>, on
    /// any other population.</param>
    internal sealed record DescriptorRecord(
        int Position,
        object? ServiceKey,
        Type? ImplementationType,
        object? ImplementationInstance,
        bool IsBrighterRegistered = false);
}
