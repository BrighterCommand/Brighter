using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Paramore.Brighter
{
    public abstract class TransformPipeline<TRequest> : IDisposable where TRequest : class, IRequest
    {
        //the mapper lease keys release on the resolution the registry opened, not the mapper instance, so a
        //shared mapper is reclaimed one resolution at a time
        protected readonly Lease<IAmAMessageMapper<TRequest>> MapperLease;
        protected readonly IReadOnlyList<Lease<IAmAMessageTransform>> TransformLeases;
        protected readonly IReadOnlyList<IAmAMessageTransform> Transforms;
        protected IAmAMessageMapper<TRequest> MessageMapper => MapperLease.Instance;
        protected TransformLifetimeScope? InstanceScope;

        private readonly IAmAMessageMapperRegistry? _mapperRegistry;
        private int _released;

        protected TransformPipeline(
            Lease<IAmAMessageMapper<TRequest>> messageMapperLease,
            IEnumerable<Lease<IAmAMessageTransform>> transformLeases,
            IAmAMessageMapperRegistry? mapperRegistry = null)
        {
            MapperLease = messageMapperLease ?? throw new ArgumentNullException(nameof(messageMapperLease));
            TransformLeases = transformLeases as IReadOnlyList<Lease<IAmAMessageTransform>> ?? transformLeases.ToArray();
            //materialise the transform instances once for execution; the leases stay for release
            Transforms = TransformLeases.Select(lease => lease.Instance).ToArray();
            _mapperRegistry = mapperRegistry;
        }

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

            //the mapper is created per pipeline, so it is ours to return; released outside InstanceScope
            //because that scope only exists when a transformer factory was supplied
            TransformPipelineDrain.Drain(
                disposeScope: () => InstanceScope?.Dispose(),
                releaseMapper: () => _mapperRegistry?.Release(MapperLease));
        }
    }
}
