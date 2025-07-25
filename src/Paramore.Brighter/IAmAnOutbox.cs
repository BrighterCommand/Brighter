using Paramore.Brighter.Observability;

namespace Paramore.Brighter
{
    /// <summary>
    /// Interface IAmAnOutbox
    /// In order to provide reliability for messages sent over a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> we
    /// store the message into an OutBox to allow later replay of those messages in the event of failure. We automatically copy any posted message into the store
    /// We provide implementations of <see cref="IAmAnOutboxSync{T}"/> for various databases. Users using other databases should consider a Pull Request
    /// </summary>
    public interface IAmAnOutbox
    {
        /// <summary>
        /// The Tracer that we want to use to capture telemetry
        /// We inject this so that we can use the same tracer as the calling application
        /// You do not need to set this property as we will set it when setting up the External Service Bus
        /// </summary>
        IAmABrighterTracer? Tracer { set; }
    }
}
