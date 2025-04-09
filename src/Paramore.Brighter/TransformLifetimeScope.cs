using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter
{
    public partial class TransformLifetimeScope : IAmATransformLifetime
    {
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<TransformLifetimeScope>();
        private readonly IAmAMessageTransformerFactory _factory;
        private readonly IList<IAmAMessageTransform> _trackedObjects = new List<IAmAMessageTransform>();

        public TransformLifetimeScope(IAmAMessageTransformerFactory factory)
        {
            _factory = factory;
        }
        
        public void Dispose()
        {
            ReleaseTrackedObjects();
            GC.SuppressFinalize(this);
        }

        ~TransformLifetimeScope()
        {
            ReleaseTrackedObjects();
        }
        
        public void Add(IAmAMessageTransform instance)
        {
            _trackedObjects.Add(instance);
            Log.TrackingInstance(s_logger, instance.GetHashCode(), instance.GetType());
         }
        
        private void ReleaseTrackedObjects()
        {
              _trackedObjects.Each((trackedItem) =>
              {
                  _factory.Release(trackedItem);
                  Log.ReleasingHandlerInstance(s_logger, trackedItem.GetHashCode(), trackedItem.GetType());
              });
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Debug, "Tracking instance {InstanceHashCode} of type {HandlerType}")]
            public static partial void TrackingInstance(ILogger logger, int instanceHashCode, Type handlerType);

            [LoggerMessage(LogLevel.Debug, "Releasing handler instance {InstanceHashCode} of type {HandlerType}")]
            public static partial void ReleasingHandlerInstance(ILogger logger, int instanceHashCode, Type handlerType);
        }
    }
}

