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
using System.Reflection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// D15's constructor-selection rule for the captive-dependency check, in one place: the public
    /// constructor with the most parameters, or - where two public constructors share the widest
    /// parameter count - none. A type with no public constructor, or only a parameterless one, also
    /// yields none.
    /// </summary>
    /// <remarks>
    /// This is a shell. The type and <see cref="Select"/>'s signature land in T7.0a, inert; D15's rule
    /// body is behaviour driven by AC-42's two constructor-selection clauses and lands in T7.5. Landing the
    /// shell here rather than in T7.5 means the captive-dependency rule does not also have to create this
    /// file. Deliberately not Microsoft's own constructor selection, which additionally requires the
    /// winner's parameters to be a superset of every other resolvable candidate's - Brighter's rule answers
    /// what a type appears to require, before anything is built, not which constructor the container would
    /// activate (ADR 0074, <c>Technology Choices</c>).
    /// </remarks>
    internal sealed class ArtefactConstructorSelector
    {
        /// <summary>
        /// Selects <paramref name="artefactType"/>'s constructor under D15's rule.
        /// </summary>
        /// <param name="artefactType">The candidate artefact type.</param>
        /// <returns>The widest public constructor, or <see langword="null"/> where two are equally wide,
        /// where there is no public constructor, or where the only public constructor is parameterless.</returns>
        /// <exception cref="NotImplementedException">Always - D15's rule has no body yet (T7.5).</exception>
        public ConstructorInfo? Select(Type artefactType) =>
            throw new NotImplementedException("D15's constructor-selection rule lands in T7.5.");
    }
}
