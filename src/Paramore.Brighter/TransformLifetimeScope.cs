using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter
{
    public class TransformLifetimeScope : IAmATransformLifetime
    {
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<TransformLifetimeScope>();
        private readonly IAmAMessageTransformerFactory _factory;
        private readonly IList<IAmAMessageTransformAsync> _trackedObjects = new List<IAmAMessageTransformAsync>();

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

       void Add(IAmAMessageTransformAsync instance)
        {
            _trackedObjects.Add(instance);
            s_logger.LogDebug("Tracking instance {InstanceHashCode} of type {HandlerType}", instance.GetHashCode(), instance.GetType());
         }

        privateleaseTrackedObjects()
        {
              _trackedObjects.Each((trackedItem) =>
              {
                  _factory.Release(trackedItem);
                  s_logger.LogDebug("Releasing handler instance {InstanceHashCode} of type {HandlerType}", trackedItem.GetHashCode(), trackedItem.GetType());
              });
        }
    }
}
