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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessageMappers;

namespace Paramore.Brighter
{
    /// <summary>
    /// Class MessageMapperRegistry
    /// In order to use a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> approach we require you to provide
    /// a <see cref="IAmAMessageMapper"/> to map between <see cref="Command"/> or <see cref="Event"/> and a <see cref="Message"/> 
    /// registered via <see cref="IAmAMessageMapperRegistry"/>
    /// This is a default implementation of<see cref="IAmAMessageMapperRegistry"/> which is suitable for most usages, the interface is provided mainly for testing
    /// </summary>
    public class MessageMapperRegistry : IAmAMessageMapperRegistry, IAmAMessageMapperRegistryAsync, IDisposable
    {
        private readonly IAmAMessageMapperFactory? _messageMapperFactory;
        private readonly IAmAMessageMapperFactoryAsync? _messageMapperFactoryAsync;
        //an int rather than a bool so Dispose can claim it with a single atomic Interlocked.Exchange:
        //an owner and the container disposing concurrently then dispose the factories exactly once
        private int _disposed;
        private readonly ConcurrentDictionary<Type, Type> _messageMappers = new();
        private readonly ConcurrentDictionary<Type, Type> _asyncMessageMappers = new();
        //resolved default mappers are cached apart from registered ones: a fallback to the default is
        //not a registration, so it must not answer "is there a mapper registered for this type?"
        private readonly ConcurrentDictionary<Type, Type> _resolvedDefaultMappers = new();
        private readonly ConcurrentDictionary<Type, Type> _resolvedDefaultAsyncMappers = new();
        private readonly Type? _defaultMessageMapper;
        private readonly Type? _defaultMessageMapperAsync;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageMapperRegistry"/> class.
        /// </summary>
        /// <param name="messageMapperFactory">The message mapper factory.</param>
        /// <param name="messageMapperFactoryAsync">The async message mapper factory</param>
        /// <param name="defaultMessageMapper">The default message Mapper</param>
        /// <param name="defaultMessageMapperAsync">The default Async Message Mapper</param>
        public MessageMapperRegistry(IAmAMessageMapperFactory? messageMapperFactory,
            IAmAMessageMapperFactoryAsync? messageMapperFactoryAsync, Type? defaultMessageMapper = null,
            Type? defaultMessageMapperAsync = null)
        {
            _messageMapperFactory = messageMapperFactory;
            _messageMapperFactoryAsync = messageMapperFactoryAsync;
            _defaultMessageMapper = defaultMessageMapper;
            _defaultMessageMapperAsync = defaultMessageMapperAsync;

            if (messageMapperFactory == null && messageMapperFactoryAsync == null)
                throw new ConfigurationException("Should have at least one factory");
        }

        /// <summary>
        /// Gets this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the t request.</typeparam>
        /// <returns>IAmAMessageMapper&lt;TRequest&gt;.</returns>
        public IAmAMessageMapper<TRequest>? Get<TRequest>() where TRequest : class, IRequest
        {
            if (_messageMapperFactory is null)
                return null;

            //only an open generic default can be closed over the request type; a non-generic default is not a
            //usable mapper for this type, so leave it null and return null — mirroring the guard in
            //ResolveMapperInfo, which HasPipeline routes through, so the two agree
            if (!_messageMappers.TryGetValue(typeof(TRequest), out var messageMapperType)
                && _defaultMessageMapper is { IsGenericTypeDefinition: true })
                messageMapperType = ResolveClosedDefault(_resolvedDefaultMappers, _defaultMessageMapper, typeof(TRequest));

            if (messageMapperType is null)
                return null;

            var mapper = _messageMapperFactory.Create(messageMapperType);
            switch (mapper)
            {
                case null:
                    return null;
                case IAmAMessageMapper<TRequest> typed:
                    return typed;
                default:
                    //the factory resolved a mapper of the wrong closed type — a Register mis-registration, or a
                    //SimpleMessageMapperFactory Func returning the wrong closed type. Release it back to the
                    //factory (and, for an IoC-backed factory, the scope it opened) before the cast failure
                    //propagates, rather than leaking the mapper and its scope for the life of the host.
                    _messageMapperFactory.Release(mapper);
                    throw new InvalidCastException(
                        $"The mapper {mapper.GetType()} registered for {typeof(TRequest)} does not implement {typeof(IAmAMessageMapper<TRequest>)}");
            }
        }

        /// <summary>
        /// Gets this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the t request.</typeparam>
        /// <returns>IAmAMessageMapperAsync&lt;TRequest&gt;.</returns>
        public IAmAMessageMapperAsync<TRequest>? GetAsync<TRequest>() where TRequest : class, IRequest
        {
            if (_messageMapperFactoryAsync is null)
                return null;
            
            //only an open generic default can be closed over the request type; a non-generic default is not a
            //usable mapper for this type, so leave it null and return null — mirroring the guard in
            //ResolveAsyncMapperInfo, which HasPipeline routes through, so the two agree
            if (!_asyncMessageMappers.TryGetValue(typeof(TRequest), out var messageMapperType)
                && _defaultMessageMapperAsync is { IsGenericTypeDefinition: true })
                messageMapperType = ResolveClosedDefault(_resolvedDefaultAsyncMappers, _defaultMessageMapperAsync, typeof(TRequest));

            if (messageMapperType is null)
                return null;

            var mapper = _messageMapperFactoryAsync.Create(messageMapperType);
            switch (mapper)
            {
                case null:
                    return null;
                case IAmAMessageMapperAsync<TRequest> typed:
                    return typed;
                default:
                    //the factory resolved a mapper of the wrong closed type — a RegisterAsync mis-registration, or a
                    //SimpleMessageMapperFactoryAsync Func returning the wrong closed type. Release it back to the
                    //factory (and, for an IoC-backed factory, the scope it opened) before the cast failure
                    //propagates, rather than leaking the mapper and its scope for the life of the host.
                    _messageMapperFactoryAsync.Release(mapper);
                    throw new InvalidCastException(
                        $"The mapper {mapper.GetType()} registered for {typeof(TRequest)} does not implement {typeof(IAmAMessageMapperAsync<TRequest>)}");
            }
        }

        /// <summary>
        /// Releases a mapper obtained from <see cref="Get{TRequest}"/> back to the factory that created it.
        /// </summary>
        /// <remarks>
        /// <see cref="Get{TRequest}"/> creates an instance per call, so an unreleased mapper leaves the
        /// factory holding it — and, for an IoC-backed factory, the scope it was resolved from — until
        /// the factory is disposed at shutdown.
        /// <para>
        /// Implemented explicitly, as is <see cref="IAmAMessageMapperRegistryAsync.Release(IAmAMessageMapperAsync)"/>:
        /// a mapper that supports both Reactor and Proactor (e.g. <c>JsonMessageMapper&lt;T&gt;</c>)
        /// implements both <see cref="IAmAMessageMapper"/> and <see cref="IAmAMessageMapperAsync"/>, so
        /// a caller holding the concrete registry could bind  <c>registry.Release(dualMapper)</c> to the wrong factory
        /// (or hit CS0121). Hiding both behind their  interfaces forces the caller to
        /// pick <see cref="IAmAMessageMapperRegistry"/> or  <see cref="IAmAMessageMapperRegistryAsync"/> explicitly,
        /// so releasing a mapper resolved from  <see cref="GetAsync{TRequest}"/> through the sync factory is a
        /// compile error, not a silent leak.
        /// </para>
        /// </remarks>
        /// <param name="mapper">The mapper to release.</param>
        void IAmAMessageMapperRegistry.Release(IAmAMessageMapper mapper)
        {
            _messageMapperFactory?.Release(mapper);
        }

        /// <summary>
        /// Releases a mapper obtained from <see cref="GetAsync{TRequest}"/> back to the factory that
        /// created it.
        /// </summary>
        /// <remarks>
        /// Synchronous; used by the pipeline finalizer fallback and build-failure cleanup. On a thread
        /// owned by the Proactor's single-threaded synchronization context prefer <see cref="ReleaseAsync"/>.
        /// <para>
        /// Implemented explicitly, as is <see cref="IAmAMessageMapperRegistry.Release(IAmAMessageMapper)"/>:
        /// with both <c>Release</c> overloads hidden behind their interfaces a caller holding the concrete
        /// registry must choose <see cref="IAmAMessageMapperRegistry"/> or
        /// <see cref="IAmAMessageMapperRegistryAsync"/>, so a dual-interface mapper cannot silently bind to
        /// the wrong factory. Release an async mapper through <see cref="IAmAMessageMapperRegistryAsync"/>
        /// or <see cref="ReleaseAsync"/>.
        /// </para>
        /// </remarks>
        /// <param name="mapper">The mapper to release.</param>
        void IAmAMessageMapperRegistryAsync.Release(IAmAMessageMapperAsync mapper)
        {
            _messageMapperFactoryAsync?.Release(mapper);
        }

        /// <summary>
        /// Releases a mapper obtained from <see cref="GetAsync{TRequest}"/> back to the factory that
        /// created it, awaiting its disposal.
        /// </summary>
        /// <remarks>
        /// The asynchronous counterpart of <see cref="IAmAMessageMapperRegistryAsync.Release(IAmAMessageMapperAsync)"/>, called from the
        /// async pipeline's <c>DisposeAsync</c>. Awaiting an <see cref="IAsyncDisposable"/> mapper's
        /// disposal rather than blocking on it keeps the Proactor pump thread free to run a continuation
        /// the mapper's <c>DisposeAsync</c> posts back to its single-threaded synchronization context.
        /// </remarks>
        /// <param name="mapper">The mapper to release.</param>
        public ValueTask ReleaseAsync(IAmAMessageMapperAsync mapper)
        {
            return _messageMapperFactoryAsync?.ReleaseAsync(mapper) ?? default;
        }

        /// <summary>
        /// Resolves the sync mapper type <see cref="Get{TRequest}"/> would create for a request type,
        /// without creating an instance.
        /// </summary>
        /// <remarks>
        /// Factory-aware, so it mirrors <see cref="Get{TRequest}"/> exactly: with no sync factory
        /// <see cref="Get{TRequest}"/> returns null, so this returns a null type too. A non-generic default
        /// mapper cannot be closed over the request type, so it resolves to null (it is not a usable mapper
        /// for this type).
        /// </remarks>
        /// <param name="requestType">The request type to look up.</param>
        /// <returns>A tuple of (mapper type or null, whether it is a default mapper).</returns>
        public (Type? MapperType, bool IsDefault) ResolveMapperInfo(Type requestType)
        {
            if (_messageMapperFactory is null)
                return (null, false);

            if (_messageMappers.TryGetValue(requestType, out var mapperType))
                return (mapperType, false);

            if (_defaultMessageMapper == null)
                return (null, false);

            if (!_defaultMessageMapper.IsGenericTypeDefinition)
                return (null, true);

            return (ResolveClosedDefault(_resolvedDefaultMappers, _defaultMessageMapper, requestType), true);
        }

        /// <summary>
        /// Resolves the async mapper type <see cref="GetAsync{TRequest}"/> would create for a request type,
        /// without creating an instance.
        /// </summary>
        /// <remarks>
        /// Factory-aware, so it mirrors <see cref="GetAsync{TRequest}"/> exactly: with no async factory
        /// <see cref="GetAsync{TRequest}"/> returns null, so this returns a null type too. A non-generic
        /// default mapper cannot be closed over the request type, so it resolves to null (it is not a usable
        /// mapper for this type).
        /// </remarks>
        /// <param name="requestType">The request type to look up.</param>
        /// <returns>A tuple of (mapper type or null, whether it is a default mapper).</returns>
        public (Type? MapperType, bool IsDefault) ResolveAsyncMapperInfo(Type requestType)
        {
            if (_messageMapperFactoryAsync is null)
                return (null, false);

            if (_asyncMessageMappers.TryGetValue(requestType, out var mapperType))
                return (mapperType, false);

            if (_defaultMessageMapperAsync == null)
                return (null, false);

            if (!_defaultMessageMapperAsync.IsGenericTypeDefinition)
                return (null, true);

            return (ResolveClosedDefault(_resolvedDefaultAsyncMappers, _defaultMessageMapperAsync, requestType), true);
        }

        /// <summary>
        /// Closes <paramref name="defaultMapper"/> over <paramref name="requestType"/>, caching the result so a
        /// repeated resolution — the hot path, since <c>HasPipeline</c> resolves the default mapper type per
        /// message — pays neither <see cref="Type.MakeGenericType"/> nor a delegate allocation. The
        /// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}.TryGetValue"/> fast path
        /// returns before <c>GetOrAdd</c> so the closure that captures <paramref name="defaultMapper"/> is only
        /// built on the (per-type, once) miss. Shared by <see cref="Get{TRequest}"/> and
        /// <see cref="ResolveMapperInfo"/> so both answer from the same cache.
        /// </summary>
        private static Type ResolveClosedDefault(
            ConcurrentDictionary<Type, Type> cache, Type defaultMapper, Type requestType)
        {
            if (cache.TryGetValue(requestType, out var closed))
                return closed;

            return cache.GetOrAdd(requestType, rt => defaultMapper.MakeGenericType(rt));
        }

        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the t request.</typeparam>
        /// <typeparam name="TMessageMapper">The type of the t message mapper.</typeparam>
        /// <exception cref="System.ArgumentException"></exception>
        public void Register<TRequest, TMessageMapper>() where TRequest : class, IRequest where TMessageMapper : class, IAmAMessageMapper<TRequest>
        {
            if (_messageMappers.ContainsKey(typeof(TRequest)))
                throw new ArgumentException(string.Format("Message type {0} already has a mapper; only one mapper can be registered per type", typeof(TRequest).Name));

            _messageMappers.TryAdd(typeof(TRequest), typeof(TMessageMapper));
        }

        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <param name="request">The type of the request to map</param>
        /// <param name="mapper">The type of the mapper for this request</param>
        /// <exception cref="System.ArgumentException"></exception>
        public void Register(Type request, Type mapper)
        {
            if (_messageMappers.ContainsKey(request))
                throw new ArgumentException(string.Format("Message type {0} already has a mapper; only one mapper can be registered per type", request.Name));

            _messageMappers.TryAdd(request, mapper);
        }
        
        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <typeparam name="TRequest">The type of the t request.</typeparam>
        /// <typeparam name="TMessageMapper">The type of the t message mapper.</typeparam>
        /// <exception cref="System.ArgumentException"></exception>
        public void RegisterAsync<TRequest, TMessageMapper>() where TRequest : class, IRequest where TMessageMapper : class, IAmAMessageMapperAsync<TRequest>
        {
            if (_asyncMessageMappers.ContainsKey(typeof(TRequest)))
                throw new ArgumentException(string.Format("Message type {0} already has a mapper; only one mapper can be registered per type", typeof(TRequest).Name));

            _asyncMessageMappers.TryAdd(typeof(TRequest), typeof(TMessageMapper));

        }
        
        /// <summary>
        /// Registers this instance.
        /// </summary>
        /// <param name="request">The type of the request to map</param>
        /// <param name="mapper">The type of the mapper for this request</param>
        /// <exception cref="System.ArgumentException"></exception>
        public void RegisterAsync(Type request, Type mapper)
        {
            if (_asyncMessageMappers.ContainsKey(request))
                throw new ArgumentException(string.Format("Message type {0} already has a mapper; only one mapper can be registered per type", request.Name));

            _asyncMessageMappers.TryAdd(request, mapper);
        }

        /// <summary>
        /// Disposes the factories this registry was built from.
        /// </summary>
        /// <remarks>
        /// The registry owns the mapper factories it was constructed with. For an IoC-backed factory each
        /// mapper obtained from <see cref="Get{TRequest}"/>/<see cref="GetAsync{TRequest}"/> but not released
        /// leaves the factory holding the <c>IServiceScope</c> it was resolved from, and nothing else
        /// can reach those factories to dispose them. An owner (the outbox producer mediator, a pipeline
        /// validator) disposes the registry at teardown so those scopes are drained rather than retained
        /// until the process exits. Idempotent and thread-safe: the disposed flag is claimed with a single
        /// atomic <see cref="System.Threading.Interlocked.Exchange(ref int,int)"/>, so an owner and the
        /// container can both dispose — even concurrently — and the factories are disposed exactly once.
        /// </remarks>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            //dispose both factories even when the first throws: a user-supplied IAmAMessageMapperFactory is
            //free to surface an exception from Dispose, and skipping the second would leak the per-resolution
            //IServiceScope it retains — the exact retention this Dispose exists to drain. The finally releases
            //the async factory before the sync factory's exception (if any) propagates to the owner.
            try
            {
                (_messageMapperFactory as IDisposable)?.Dispose();
            }
            finally
            {
                (_messageMapperFactoryAsync as IDisposable)?.Dispose();
            }
        }
    }
}
