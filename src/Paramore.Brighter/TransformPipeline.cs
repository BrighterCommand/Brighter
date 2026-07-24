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
        protected IAmAMessageMapper<TRequest> MessageMapper = messageMapper;
        protected IEnumerable<IAmAMessageTransform> Transforms = transforms;
        protected TransformLifetimeScope? InstanceScope;

        private readonly IAmAMessageMapperRegistry? _mapperRegistry = mapperRegistry;
        private int _released;

        /// <summary>
        /// Disposes a pipeline builder, which will call release on the factory for the message mapper and
        /// any transforms generated for the pipeline
        /// </summary>
        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes a pipeline builder, which will call release on the factory for the message mapper and
        /// any transforms generated for the pipeline
        /// </summary>
        ~TransformPipeline()
        {
            //a finalizer must never let an exception escape — that terminates the process. Releasing a
            //scope that holds an IAsyncDisposable-only mapper/transform through the synchronous path can
            //throw (MS DI's sync scope Dispose throws for an async-only service), as can a user
            //Dispose/DisposeAsync. Release best-effort here and swallow; an explicit Dispose still
            //surfaces the exception to the caller who owns the pipeline.
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
