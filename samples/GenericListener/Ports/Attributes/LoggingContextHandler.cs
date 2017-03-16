using Paramore.Brighter;
using Paramore.Brighter.Logging.Attributes;

namespace GenericListener.Ports.Attributes
{
    public class LoggingContextHandler<TRequest> : RequestHandler<TRequest> where TRequest : class, IRequest
    {
        [RequestLogging(0, HandlerTiming.Before)]
        public override TRequest Handle(TRequest command)
        {
            return base.Handle(command);
        }
    }
}
