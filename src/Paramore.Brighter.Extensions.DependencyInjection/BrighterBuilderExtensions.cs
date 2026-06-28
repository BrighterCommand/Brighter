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
    /// Extension methods on <see cref="IBrighterBuilder"/> that are deliberately kept off the
    /// interface so they can be added without making a binary-breaking change to public API for
    /// downstream implementers.
    /// </summary>
    public static class BrighterBuilderExtensions
    {
        /// <summary>
        /// Register transforms with the built-in transformer registry. Symmetric with
        /// <see cref="IBrighterBuilder.Handlers"/> and <see cref="IBrighterBuilder.MapperRegistry"/>,
        /// intended for callers that want to add transforms explicitly (for example from a
        /// source generator) rather than via assembly scanning.
        /// </summary>
        /// <param name="builder">The Brighter builder.</param>
        /// <param name="registerTransforms">A callback to register transforms.</param>
        /// <returns>The builder, to allow chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <paramref name="builder"/> is not the <see cref="ServiceCollectionBrighterBuilder"/>
        /// implementation that owns a <see cref="ServiceCollectionTransformerRegistry"/>.
        /// </exception>
        public static IBrighterBuilder Transforms(this IBrighterBuilder builder, Action<ServiceCollectionTransformerRegistry> registerTransforms)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (registerTransforms == null) throw new ArgumentNullException(nameof(registerTransforms));

            if (builder is ServiceCollectionBrighterBuilder concrete)
                return concrete.Transforms(registerTransforms);

            throw new InvalidOperationException(
                $"The {nameof(Transforms)} extension requires {nameof(ServiceCollectionBrighterBuilder)}; received {builder.GetType().FullName}.");
        }
    }
}
