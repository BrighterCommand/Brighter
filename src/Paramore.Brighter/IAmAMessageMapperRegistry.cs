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

namespace Paramore.Brighter
{
    /// <summary>
    /// Interface IAmAMessageMapperRegistry
    /// In order to use a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> approach we require you to provide
    /// a <see cref="IAmAMessageMapper"/> to map between <see cref="Command"/> or <see cref="Event"/> and a <see cref="Message"/> 
    /// registered via <see cref="IAmAMessageMapperRegistry"/>
    /// The default implementation<see cref="MessageMapperRegistry"/> is suitable for most purposes and the interface is provided for testing
    /// </summary>
    public interface IAmAMessageMapperRegistry
    {
        /// <summary>
        /// Gets a mapper for <typeparamref name="T"/>, wrapped in a <see cref="Lease{T}"/> that identifies this
        /// resolution so it can later be released back to the factory that created it.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>A lease over the mapper, or <c>null</c> if none is registered.</returns>
        Lease<IAmAMessageMapper<T>>? Get<T>() where T : class, IRequest;
        /// <summary>
        /// Resolves the mapper type that <see cref="Get{T}"/> would create for <paramref name="requestType"/>,
        /// without creating an instance — so a caller that only needs to know whether a mapper exists (e.g.
        /// a pipeline probe) does not pay for one.
        /// </summary>
        /// <remarks>
        /// Mirrors <see cref="Get{T}"/>'s resolution, including its guards: a null <c>MapperType</c> means
        /// <see cref="Get{T}"/> would also return null — no mapper is registered and no usable default
        /// applies, or the registry has no factory to create one.
        /// </remarks>
        /// <param name="requestType">The request type to resolve a mapper for.</param>
        /// <returns>The resolved mapper type (null if none), and whether it came from the default mapper.</returns>
        (Type? MapperType, bool IsDefault) ResolveMapperInfo(Type requestType);
        /// <summary>
        /// Releases a mapper lease obtained from <see cref="Get{T}"/> back to the factory that created it.
        /// </summary>
        /// <remarks>
        /// <see cref="Get{T}"/> creates an instance on every call, so every caller must release the lease it
        /// obtains once it has finished with it — including a caller that only wanted to know whether a
        /// mapper exists. Releasing by lease keys on the resolution, so a shared mapper instance resolved under
        /// a transient lifetime is reclaimed one resolution at a time and an over-release is a no-op.
        /// </remarks>
        /// <param name="lease">The lease returned by <see cref="Get{T}"/> to release.</param>
        void Release<T>(Lease<IAmAMessageMapper<T>> lease) where T : class, IRequest;
        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the t request.</typeparam>
        /// <typeparam name="TMessageMapper">The type of the t message mapper.</typeparam>
        void Register<TRequest, TMessageMapper>() where TRequest : class, IRequest where TMessageMapper : class, IAmAMessageMapper<TRequest>;

        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <param name="request">The type of the request to map</param>
        /// <param name="mapper">The type of the mapper for this request</param>
        /// <exception cref="System.ArgumentException"></exception>
        void Register(Type request, Type mapper);
    }
}
