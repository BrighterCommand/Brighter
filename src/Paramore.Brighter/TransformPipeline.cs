using System;
using System.Collections.Generic;

namespace Paramore.Brighter
{
    public abstract class TransformPipeline<TRequest>(
        IAmAMessageMapper<TRequest> messageMapper,
        IEnumerable<IAmAMessageTransform> transforms) : IDisposable where TRequest : class, IRequest
    {
        protected IAmAMessageMapper<TRequest> MessageMapper = messageMapper;
        protected IEnumerable<IAmAMessageTransform> Transforms = transforms;
        protected TransformLifetimeScope? InstanceScope;

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
