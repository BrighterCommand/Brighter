using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter
{
    public partial class TransformLifetimeScopeAsync : IAmATransformLifetimeAsync
    {
        private readonly ILogger _logger;
        private readonly IAmAMessageTransformerFactoryAsync _factory;
        private readonly IList<IAmAMessageTransformAsync> _trackedObjects = new List<IAmAMessageTransformAsync>();

        public TransformLifetimeScopeAsync(IAmAMessageTransformerFactoryAsync factory, ILoggerFactory? loggerFactory = null)
        {
            _factory = factory;
            _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<TransformLifetimeScope>();
        }
        
        public void Dispose()
        {
            ReleaseTrackedObjects();
            GC.SuppressFinalize(this);
        }

        ~TransformLifetimeScopeAsync()
        {
            ReleaseTrackedObjects();
        }
        
        public void Add(IAmAMessageTransformAsync instance)
        {
            _trackedObjects.Add(instance);
            Log.TrackingInstance(_logger, instance.GetHashCode(), instance.GetType());
         }
        
        private void ReleaseTrackedObjects()
        {
              _trackedObjects.Each((trackedItem) =>
              {
                  _factory.Release(trackedItem);
                  Log.ReleasingHandlerInstance(_logger, trackedItem.GetHashCode(), trackedItem.GetType());
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

