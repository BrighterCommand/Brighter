using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    public abstract class TransformPipelineAsync<TRequest>(
        IAmAMessageMapperAsync<TRequest> messageMapper,
        IEnumerable<IAmAMessageTransformAsync> transforms,
        IAmAMessageMapperRegistryAsync? mapperRegistry = null)  : IDisposable, IAsyncDisposable where TRequest : class, IRequest
    {
        protected IAmAMessageMapperAsync<TRequest> MessageMapper = messageMapper;
        protected IEnumerable<IAmAMessageTransformAsync> Transforms = transforms;
        protected TransformLifetimeScopeAsync? InstanceScope;

        private readonly IAmAMessageMapperRegistryAsync? _mapperRegistry = mapperRegistry;
        private int _released;

        /// <summary>
        /// Disposes the pipeline, releasing the message mapper and any transforms back to their factories.
        /// <para>
        /// This is the synchronous fallback — the finalizer, or a caller that used <c>using</c> rather
        /// than <c>await using</c>. On a thread owned by the Proactor's single-threaded synchronization
        /// context prefer <see cref="DisposeAsync"/>: releasing an <see cref="IAsyncDisposable"/>
        /// mapper/transform synchronously drains it through a blocking wait (offloaded to a pool thread to
        /// avoid deadlock, but still a stall), whereas <see cref="DisposeAsync"/> awaits it without
        /// blocking the pump.
        /// </para>
        /// </summary>
        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the pipeline asynchronously, releasing the message mapper and any transforms back to
        /// their factories and awaiting their disposal. Preferred on the Proactor pump thread: an
        /// <see cref="IAsyncDisposable"/> mapper/transform is awaited rather than blocked on, so a
        /// continuation its <c>DisposeAsync</c> posts back to the single-threaded synchronization context
        /// can run.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            //release once only; shares the guard with Dispose so an explicit dispose followed by another
            //(in either form) must not release twice
            if (Interlocked.Exchange(ref _released, 1) != 0) return;

            if (InstanceScope is not null)
                await InstanceScope.DisposeAsync().ConfigureAwait(false);

            //the mapper is created per pipeline, so it is ours to return; released outside InstanceScope
            //because that scope only exists when a transformer factory was supplied
            if (_mapperRegistry is not null)
                await _mapperRegistry.ReleaseAsync(MessageMapper).ConfigureAwait(false);

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes a pipeline builder, which will call release on the factory for the message mapper and
        /// any transforms generated for the pipeline
        /// </summary>
        ~TransformPipelineAsync()
        {
            //a finalizer must never let an exception escape — that terminates the process. Releasing a
            //scope that holds an IAsyncDisposable-only mapper/transform through the synchronous path can
            //throw (MS DI's sync scope Dispose throws for an async-only service), as can a user
            //Dispose/DisposeAsync. Release best-effort here and swallow; an explicit Dispose/DisposeAsync
            //still surfaces the exception to the caller who owns the pipeline.
            try { ReleaseUnmanagedResources(); }
            catch { /* swallowed: a finalizer must not throw */ }
        }

        private void ReleaseUnmanagedResources()
        {
            //release once only; an explicit Dispose followed by another must not release twice
            if (Interlocked.Exchange(ref _released, 1) != 0) return;

            InstanceScope?.Dispose();

            //the mapper is created per pipeline, so it is ours to return; released outside InstanceScope
            //because that scope only exists when a transformer factory was supplied
            _mapperRegistry?.Release(MessageMapper);
        }
    }
}
