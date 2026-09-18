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
    /// Deliberately not Microsoft's own constructor selection, which additionally requires the winner's
    /// parameters to be a superset of every other resolvable candidate's - Brighter's rule answers what a
    /// type appears to require, before anything is built, not which constructor the container would
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
        public ConstructorInfo? Select(Type artefactType)
        {
            var publicConstructors = artefactType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (publicConstructors.Length == 0)
                return null;

            var widest = publicConstructors[0];
            var widestCount = widest.GetParameters().Length;
            var tied = false;

            for (var i = 1; i < publicConstructors.Length; i++)
            {
                var count = publicConstructors[i].GetParameters().Length;
                if (count > widestCount)
                {
                    widest = publicConstructors[i];
                    widestCount = count;
                    tied = false;
                }
                else if (count == widestCount)
                {
                    tied = true;
                }
            }

            if (tied || widestCount == 0)
                return null;

            return widest;
        }
    }
}
