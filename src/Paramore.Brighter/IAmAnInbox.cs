using Paramore.Brighter.Observability;

namespace Paramore.Brighter
{
    public interface IAmAnInbox
    {
        /// <summary>
        /// The Tracer that we want to use to capture telemetry
        /// We inject this so that we can use the same tracer as the calling application
        /// You do not need to set this property as we will set it when setting up the Service Activator
        /// </summary>
        IAmABrighterTracer Tracer { set; }
    }
}
