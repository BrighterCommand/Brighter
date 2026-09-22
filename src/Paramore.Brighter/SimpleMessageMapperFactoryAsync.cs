#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// Class SimpleMessageMapperFactory.
    /// This allows you to return a simple function that finds a given message mapper. Intended for lightweight message mapping,
    /// such as with a ControlBusSender. We recommend you wrap your IoC container for heavyweight mapping.
    /// </summary>
    public class SimpleMessageMapperFactoryAsync : IAmAMessageMapperFactoryAsync
    {
        private readonly Func<Type, IAmAMessageMapperAsync> _factoryMethod;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleMessageMapperFactory"/> class.
        /// </summary>
        /// <param name="factoryMethod">The factory method.</param>
        public SimpleMessageMapperFactoryAsync(Func<Type, IAmAMessageMapperAsync> factoryMethod)
        {
            _factoryMethod = factoryMethod;
        }

        /// <summary>
        /// Creates the specified message mapper type.
        /// </summary>
        /// <param name="messageMapperType">Type of the message mapper.</param>
        /// <returns>IAmAMessageMapper.</returns>
        public Lease<IAmAMessageMapperAsync>? Create(Type messageMapperType)
        {
            var mapper = _factoryMethod(messageMapperType);
            return mapper is null ? null : Lease<IAmAMessageMapperAsync>.Untracked(mapper);
        }

        /// <summary>
        /// Releases the specified message mapper lease. A no-op: the factory method supplied by the caller
        /// owns whatever it returns — it may legitimately hand back a shared instance — so this factory
        /// must not dispose it, and the lease carries no release token.
        /// </summary>
        /// <param name="lease">The mapper lease to release.</param>
        public void Release(Lease<IAmAMessageMapperAsync>? lease)
        {
        }

        /// <summary>
        /// Releases the specified message mapper lease asynchronously. A no-op for the same reason as
        /// <see cref="Release"/>: the caller's factory method owns whatever it returns.
        /// </summary>
        /// <param name="lease">The mapper lease to release.</param>
        public ValueTask ReleaseAsync(Lease<IAmAMessageMapperAsync>? lease)
        {
            return default;
        }
    }
}
