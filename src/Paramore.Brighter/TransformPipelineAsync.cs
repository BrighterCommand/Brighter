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
        protected readonly IAmAMessageMapperAsync<TRequest> MessageMapper = messageMapper;
        protected readonly IEnumerable<IAmAMessageTransformAsync> Transforms = transforms;
        protected TransformLifetimeScopeAsync? InstanceScope;

        private int _released;

        /// <summary>
        /// Disposes the pipeline, releasing the message mapper and any transforms back to their factories.
        /// <para>
        /// This is the synchronous fallback — the finalizer, or a caller that used <c>using</c> rather
        /// than <c>await using</c>. On a thread owned by the Proactor's single-threaded synchronization
        /// context prefer <see cref="DisposeAsync"/>: releasing an <see cref="IAsyncDisposable"/>
        /// mapper/transform synchronously drains it through a blocking wait (the pump context is suppressed
        /// for the wait to avoid deadlock, but it is still a stall), whereas <see cref="DisposeAsync"/>
        /// awaits it without blocking the pump.
        /// </para>
        /// </summary>
        public void Dispose()
        {
            //SuppressFinalize in a finally: if the release throws (explicit Dispose still surfaces it),
            //the object would otherwise stay registered for finalization, and the finalizer's retry only
            //returns on the release-once guard — a wasted finalization
            try { ReleaseUnmanagedResources(); }
            finally { GC.SuppressFinalize(this); }
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

            //SuppressFinalize in an outer finally so it runs even if the release throws (an explicit
            //DisposeAsync still surfaces the exception); otherwise the object stays registered for
            //finalization and the finalizer's retry only returns on the release-once guard — wasted work
            try
            {
                try
                {
                    if (InstanceScope is not null)
                        await InstanceScope.DisposeAsync().ConfigureAwait(false);
                }
                finally
                {
                    //the mapper is created per pipeline, so it is ours to return; released outside InstanceScope
                    //because that scope only exists when a transformer factory was supplied. Released in a
                    //finally so a throw from the transform-scope disposal above cannot orphan the mapper's own
                    //scope — the release-once guard is already set, so no later dispose or the finalizer would
                    //retry it, which is the exact leak this pipeline is meant to close.
                    if (mapperRegistry is not null)
                        await mapperRegistry.ReleaseAsync(MessageMapper).ConfigureAwait(false);
                }
            }
            finally
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Disposes a pipeline builder, which will call release on the factory for the message mapper and
        /// any transforms generated for the pipeline
        /// </summary>
        ~TransformPipelineAsync()
        {
            //a finalizer must never let an exception escape — that terminates the process. Releasing a
            //scope that holds an IAsyncDisposable-only mapper/transform through the synchronous path can
            //throw (netstandard2.0 only: MS DI's sync scope Dispose throws for an async-only service; net8+
            //takes the async branch first), as can a user
            //Dispose/DisposeAsync. Release best-effort here and swallow; an explicit Dispose/DisposeAsync
            //still surfaces the exception to the caller who owns the pipeline.
            try { ReleaseUnmanagedResources(); }
            catch { /* swallowed: a finalizer must not throw */ }
        }

        private void ReleaseUnmanagedResources()
        {
            //release once only; an explicit Dispose followed by another must not release twice
            if (Interlocked.Exchange(ref _released, 1) != 0) return;

            try
            {
                InstanceScope?.Dispose();
            }
            finally
            {
                //the mapper is created per pipeline, so it is ours to return; released outside InstanceScope
                //because that scope only exists when a transformer factory was supplied. Released in a
                //finally so a throw from the transform-scope disposal above cannot orphan the mapper's own
                //scope — the release-once guard is already set, so neither the finalizer nor a later Dispose
                //would retry it, which is the exact leak this pipeline is meant to close.
                mapperRegistry?.Release(MessageMapper);
            }
        }
    }
}
