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
            //a finalizer must never let an exception escape — that terminates the process. Releasing a
            //transform whose scope holds an IAsyncDisposable-only service through the synchronous path can
            //throw (MS DI's sync scope Dispose throws for an async-only service), as can a user Dispose.
            //Finalization order is non-deterministic, so this scope can be finalized before its owning
            //pipeline disposes it. Release best-effort here and swallow; an explicit Dispose still
            //surfaces the exception to the owner.
            try { ReleaseTrackedObjects(); }
            catch { /* swallowed: a finalizer must not throw */ }
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

