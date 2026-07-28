using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter
{
    public partial class TransformLifetimeScopeAsync(IAmAMessageTransformerFactoryAsync factory)
        : IAmATransformLifetimeAsync
    {
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<TransformLifetimeScopeAsync>();
        private readonly IList<Lease<IAmAMessageTransformAsync>> _trackedObjects = new List<Lease<IAmAMessageTransformAsync>>();

        public void Dispose()
        {
            //SuppressFinalize in a finally: the drain can now throw (a Release failure surfaces as an
            //AggregateException to an explicit Dispose), and if it does the object would otherwise stay
            //registered for finalization, whose retry only re-runs the already-drained list — wasted work
            try { ReleaseTrackedObjects(); }
            finally { GC.SuppressFinalize(this); }
        }

        /// <summary>
        /// Releases every tracked transform asynchronously, awaiting each release. Used by the async
        /// pipeline's <c>DisposeAsync</c> so an <see cref="IAsyncDisposable"/> transform's disposal is
        /// awaited rather than blocked on — keeping the Proactor pump thread free.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            //SuppressFinalize in a finally: the drain can now throw (a Release failure surfaces as an
            //AggregateException to an explicit DisposeAsync), and if it does the object would otherwise stay
            //registered for finalization, whose retry only re-runs the already-drained list — wasted work
            try { await ReleaseTrackedObjectsAsync().ConfigureAwait(false); }
            finally { GC.SuppressFinalize(this); }
        }

        ~TransformLifetimeScopeAsync()
        {
            //a finalizer must never let an exception escape — that terminates the process. Releasing a
            //transform whose scope holds an IAsyncDisposable-only service through the synchronous path can
            //throw (netstandard2.0 only: MS DI's sync scope Dispose throws for an async-only service; net8+
            //takes the async branch first), as can a user Dispose.
            //Finalization order is non-deterministic, so this scope can be finalized before its owning
            //pipeline disposes it. Release best-effort here and swallow; an explicit Dispose/DisposeAsync
            //still surfaces the exception to the owner.
            try { ReleaseTrackedObjects(); }
            catch { /* swallowed: a finalizer must not throw */ }
        }

        public void Add(Lease<IAmAMessageTransformAsync> lease)
        {
            _trackedObjects.Add(lease);
            Log.TrackingInstance(s_logger, lease.Instance.GetHashCode(), lease.Instance.GetType());
         }

        private void ReleaseTrackedObjects()
        {
            //drain as we go — see TransformLifetimeScope.ReleaseTrackedObjects: removing each transform
            //before releasing it stops a throwing Release from skipping the rest on a finalizer retry or
            //re-releasing an already-released transform. This synchronous path backs the finalizer.
            //Remove from the tail so each removal is O(1); release order is irrelevant, the transforms are independent.
            //A throwing Release is caught per item so the drain completes deterministically rather than
            //aborting; the collected failures surface together as an AggregateException (the finalizer swallows it).
            List<Exception>? releaseExceptions = null;
            while (_trackedObjects.Count > 0)
            {
                var lastIndex = _trackedObjects.Count - 1;
                var trackedItem = _trackedObjects[lastIndex];
                _trackedObjects.RemoveAt(lastIndex);
                try
                {
                    factory.Release(trackedItem);
                    Log.ReleasingHandlerInstance(s_logger, trackedItem.Instance.GetHashCode(), trackedItem.Instance.GetType());
                }
                catch (Exception ex)
                {
                    (releaseExceptions ??= new List<Exception>()).Add(ex);
                }
            }

            if (releaseExceptions is not null)
                throw new AggregateException(releaseExceptions);
        }

        private async ValueTask ReleaseTrackedObjectsAsync()
        {
            //drain as we go, same reasoning as the synchronous path: a ReleaseAsync that throws must not
            //skip the remaining transforms on a retry, nor let an already-released transform be released
            //again when DisposeAsync is followed by the finalizer's synchronous release.
            //Remove from the tail so each removal is O(1); release order is irrelevant, the transforms are independent.
            //A throwing ReleaseAsync is caught per item so the drain completes deterministically rather than
            //aborting; the collected failures surface together as an AggregateException to an explicit DisposeAsync.
            List<Exception>? releaseExceptions = null;
            while (_trackedObjects.Count > 0)
            {
                var lastIndex = _trackedObjects.Count - 1;
                var trackedItem = _trackedObjects[lastIndex];
                _trackedObjects.RemoveAt(lastIndex);
                try
                {
                    await factory.ReleaseAsync(trackedItem).ConfigureAwait(false);
                    Log.ReleasingHandlerInstance(s_logger, trackedItem.Instance.GetHashCode(), trackedItem.Instance.GetType());
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

