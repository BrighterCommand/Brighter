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
    /// Answers, for one ambient offered by an <see cref="IAmAScopeProvider"/>, whether this container
    /// package may resolve from it. Shared by all five container-backed factories, so there is one
    /// implementation of the usability test rather than five copies of it.
    /// </summary>
    internal static class AmbientScopeProbe
    {
        /// <summary>
        /// Tests whether <paramref name="ambient"/>'s resolution source is one this package may borrow
        /// from: live, Microsoft-shaped, and not the calling factory's own root provider.
        /// </summary>
        /// <param name="ambient">The offered ambient.</param>
        /// <param name="root">The root <see cref="IServiceProvider"/> the calling factory was
        /// constructed with.</param>
        /// <returns>
        /// <see langword="false"/> where <see cref="IAmAServiceProviderScope.Services"/> is
        /// <see langword="null"/>, throws, or is reference-equal to <paramref name="root"/>; where it
        /// offers no <see cref="IServiceScopeFactory"/>; where it offers no
        /// <see cref="ScopedArtefactCache"/> (the container it came from was never registered into by
        /// Brighter); or where either resolution throws. <see langword="true"/> otherwise.
        /// </returns>
        public static bool CanResolveFrom(IAmAServiceProviderScope ambient, IServiceProvider root)
        {
            try
            {
                var services = ambient.Services;
                if (services is null) return false;
                if (ReferenceEquals(services, root)) return false;
                if (services.GetService(typeof(IServiceScopeFactory)) is null) return false;
                if (services.GetService(typeof(ScopedArtefactCache)) is null) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
