using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter
{
    public partial class TransformLifetimeScope(IAmAMessageTransformerFactory factory) : IAmATransformLifetime
    {
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<TransformLifetimeScope>();
        private readonly IList<IAmAMessageTransform> _trackedObjects = new List<IAmAMessageTransform>();

        public void Dispose()
        {
            ReleaseTrackedObjects();
            GC.SuppressFinalize(this);
        }

        ~TransformLifetimeScope()
        {
            //a finalizer must never let an exception escape — that terminates the process. Releasing a
            //transform whose scope holds an IAsyncDisposable-only service through the synchronous path can
            //throw (netstandard2.0 only: MS DI's sync scope Dispose throws for an async-only service; net8+
            //takes the async branch first), as can a user Dispose.
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
            //drain as we go: remove each transform before releasing it, so a Release that throws (MS DI's
            //sync scope Dispose throws for an IAsyncDisposable-only transform, and a user Dispose may throw)
            //neither leaves the remaining transforms unreleased on a finalizer retry nor lets an
            //already-released transform be released again — the retry re-runs this over the shortened list.
            //Remove from the tail so each removal is O(1); release order is irrelevant, the transforms are independent.
            //A throwing Release is caught per item so the drain completes deterministically rather than
            //aborting and leaving the remaining transforms to the GC-timed finalizer; the collected failures
            //surface together as an AggregateException to an explicit Dispose (the finalizer swallows it).
            List<Exception>? releaseExceptions = null;
            while (_trackedObjects.Count > 0)
            {
                var lastIndex = _trackedObjects.Count - 1;
                var trackedItem = _trackedObjects[lastIndex];
                _trackedObjects.RemoveAt(lastIndex);
                try
                {
                    factory.Release(trackedItem);
                    Log.ReleasingHandlerInstance(s_logger, trackedItem.GetHashCode(), trackedItem.GetType());
                }
                catch (Exception ex)
                {
                    (releaseExceptions ??= new List<Exception>()).Add(ex);
                }
            }

            if (releaseExceptions is not null)
                throw new AggregateException(releaseExceptions);
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

