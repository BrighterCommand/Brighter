using System;
using System.Collections.Generic;

namespace Paramore.Brighter
{
    public abstract class TransformPipeline<TRequest> : IDisposable where TRequest : class, IRequest, new()
    {
        protected IAmAMessageMapper<TRequest> MessageMapper;
        protected IEnumerable<IAmAMessageTransformAsync> Transforms;
        protected TransformLifetimeScope InstanceScope;

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
        ~TransformPipeline()
        {
            ReleaseUnmanagedResources();
        }

        private void ReleaseUnmanagedResources()
        {
            InstanceScope?.Dispose();
        }
    }
}
