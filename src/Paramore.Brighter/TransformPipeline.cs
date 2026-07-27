using System;
using System.Collections.Generic;
using System.Threading;

namespace Paramore.Brighter
{
    public abstract class TransformPipeline<TRequest>(
        IAmAMessageMapper<TRequest> messageMapper,
        IEnumerable<IAmAMessageTransform> transforms,
        IAmAMessageMapperRegistry? mapperRegistry = null) : IDisposable where TRequest : class, IRequest
    {
        protected readonly IAmAMessageMapper<TRequest> MessageMapper = messageMapper;
        protected readonly IEnumerable<IAmAMessageTransform> Transforms = transforms;
        protected TransformLifetimeScope? InstanceScope;

        private int _released;

        /// <summary>
        /// Disposes a pipeline builder, which will call release on the factory for the message mapper and
        /// any transforms generated for the pipeline
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
        /// Disposes a pipeline builder, which will call release on the factory for the message mapper and
        /// any transforms generated for the pipeline
        /// </summary>
        ~TransformPipeline()
        {
            //a finalizer must never let an exception escape — that terminates the process. Releasing a
            //scope that holds an IAsyncDisposable-only mapper/transform through the synchronous path can
            //throw (netstandard2.0 only: MS DI's sync scope Dispose throws for an async-only service; net8+
            //takes the async branch first), as can a user
            //Dispose/DisposeAsync. Release best-effort here and swallow; an explicit Dispose still
            //surfaces the exception to the caller who owns the pipeline.
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
