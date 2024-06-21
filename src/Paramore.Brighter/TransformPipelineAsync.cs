using System;
using System.Collections.Generic;

namespace Paramore.Brighter
{
    public abstract class TransformPipelineAsync<TRequest> : IDisposable where TRequest : class, IRequest
    {
        protected IAmAMessageMapperAsync<TRequest> MessageMapper;
        protected IEnumerable<IAmAMessageTransformAsync> Transforms;
        protected TransformLifetimeScopeAsync InstanceScope;

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
