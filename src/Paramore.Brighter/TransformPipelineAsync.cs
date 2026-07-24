using System;
using System.Collections.Generic;
using System.Threading;

namespace Paramore.Brighter
{
    public abstract class TransformPipelineAsync<TRequest>(
        IAmAMessageMapperAsync<TRequest> messageMapper,
        IEnumerable<IAmAMessageTransformAsync> transforms,
        IAmAMessageMapperRegistryAsync? mapperRegistry = null)  : IDisposable where TRequest : class, IRequest
    {
        protected IAmAMessageMapperAsync<TRequest> MessageMapper = messageMapper;
        protected IEnumerable<IAmAMessageTransformAsync> Transforms = transforms;
        protected TransformLifetimeScopeAsync? InstanceScope;

        private readonly IAmAMessageMapperRegistryAsync? _mapperRegistry = mapperRegistry;
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
        ~TransformPipelineAsync()
        {
            ReleaseUnmanagedResources();
        }

        private void ReleaseUnmanagedResources()
        {
            //release once only; an explicit Dispose followed by another must not release twice
            if (Interlocked.Exchange(ref _released, 1) != 0) return;

            InstanceScope?.Dispose();

            //the mapper is created per pipeline, so it is ours to return; released outside InstanceScope
            //because that scope only exists when a transformer factory was supplied
            _mapperRegistry?.ReleaseAsync(MessageMapper);
        }
    }
}
