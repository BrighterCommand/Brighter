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
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// One candidate handler, mapper or transform found in the container registration snapshot (ADR 0074
    /// step 2) - its type, which of the three kinds it presents as, and the configured lifetime that kind
    /// selects. A type presenting more than one kind (for example a type that is both a handler and a
    /// mapper) yields one <see cref="ArtefactRegistration"/> per kind.
    /// </summary>
    /// <remarks>
    /// A record rather than three positional parameters of the same shape, because <see cref="ArtefactKind"/>
    /// and <see cref="ServiceLifetime"/> are exactly the kind of same-family values a parameter list invites
    /// transposing (ADR 0074, <c>Technology Choices</c>).
    /// </remarks>
    /// <param name="ArtefactType">The candidate's implementation type.</param>
    /// <param name="Kind">Which of Brighter's three per-pipeline artefacts this candidate presents as.</param>
    /// <param name="ConfiguredLifetime">The <see cref="IBrighterOptions"/> lifetime <paramref name="Kind"/>
    /// selects - <c>HandlerLifetime</c>, <c>MapperLifetime</c> or <c>TransformerLifetime</c>.</param>
    internal sealed record ArtefactRegistration(Type ArtefactType, ArtefactKind Kind, ServiceLifetime ConfiguredLifetime);
}
