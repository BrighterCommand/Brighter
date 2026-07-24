using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter
{
    public partial class TransformLifetimeScopeAsync : IAmATransformLifetimeAsync
    {
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<TransformLifetimeScope>();
        private readonly IAmAMessageTransformerFactoryAsync _factory;
        private readonly IList<IAmAMessageTransformAsync> _trackedObjects = new List<IAmAMessageTransformAsync>();

        public TransformLifetimeScopeAsync(IAmAMessageTransformerFactoryAsync factory)
        {
            _factory = factory;
        }
        
        public void Dispose()
        {
            ReleaseTrackedObjects();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases every tracked transform asynchronously, awaiting each release. Used by the async
        /// pipeline's <c>DisposeAsync</c> so an <see cref="IAsyncDisposable"/> transform's disposal is
        /// awaited rather than blocked on — keeping the Proactor pump thread free.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await ReleaseTrackedObjectsAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        ~TransformLifetimeScopeAsync()
        {
            //a finalizer must never let an exception escape — that terminates the process. Releasing a
            //transform whose scope holds an IAsyncDisposable-only service through the synchronous path can
            //throw (MS DI's sync scope Dispose throws for an async-only service), as can a user Dispose.
            //Finalization order is non-deterministic, so this scope can be finalized before its owning
            //pipeline disposes it. Release best-effort here and swallow; an explicit Dispose/DisposeAsync
            //still surfaces the exception to the owner.
            try { ReleaseTrackedObjects(); }
            catch { /* swallowed: a finalizer must not throw */ }
        }

        public void Add(IAmAMessageTransformAsync instance)
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

        private async ValueTask ReleaseTrackedObjectsAsync()
        {
            foreach (var trackedItem in _trackedObjects)
            {
                await _factory.ReleaseAsync(trackedItem).ConfigureAwait(false);
                Log.ReleasingHandlerInstance(s_logger, trackedItem.GetHashCode(), trackedItem.GetType());
            }
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

