using System;
using System.Collections.Generic;

namespace Paramore.Brighter
{
    public abstract class TransformPipelineAsync<TRequest>(
        IAmAMessageMapperAsync<TRequest> messageMapper,
        IEnumerable<IAmAMessageTransformAsync> transforms)  : IDisposable where TRequest : class, IRequest
    {
        protected IAmAMessageMapperAsync<TRequest> MessageMapper = messageMapper;
        protected IEnumerable<IAmAMessageTransformAsync> Transforms = transforms;
        protected TransformLifetimeScopeAsync? InstanceScope;

        /// <summary>
        /// Disposes a pipeline builder, which will call release on the factory for any transforms generated for the pipeline 
        /// </summary>
        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes a pipeline builder, which will call release on the factory for any transforms generated for the pipeline 
        /// </summary>
        ~TransformPipelineAsync()
        {
            ReleaseUnmanagedResources();
        }

        private void ReleaseUnmanagedResources()
        {
            InstanceScope?.Dispose();
        }
    }
}
