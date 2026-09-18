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
    /// Which of Brighter's three per-pipeline artefacts a candidate registration is, and therefore which
    /// <see cref="IBrighterOptions"/> lifetime governs it (ADR 0074 step 2).
    /// </summary>
    /// <remarks>
    /// A candidate is assigned a kind by which marker interface its implementation type implements -
    /// <see cref="IHandleRequests"/>/<see cref="IHandleRequestsAsync"/> for <see cref="Handler"/>,
    /// <see cref="IAmAMessageMapper"/>/<see cref="IAmAMessageMapperAsync"/> for <see cref="Mapper"/>, and
    /// <see cref="IAmAMessageTransform"/>/<see cref="IAmAMessageTransformAsync"/> for <see cref="Transform"/>.
    /// A type presenting more than one marker is assigned each kind it presents, as a separate
    /// <see cref="ArtefactRegistration"/> per kind.
    /// </remarks>
    internal enum ArtefactKind
    {
        /// <summary>Governed by <see cref="IBrighterOptions.HandlerLifetime"/>.</summary>
        Handler,

        /// <summary>Governed by <see cref="IBrighterOptions.MapperLifetime"/>.</summary>
        Mapper,

        /// <summary>Governed by <see cref="IBrighterOptions.TransformerLifetime"/>.</summary>
        Transform
    }
}
