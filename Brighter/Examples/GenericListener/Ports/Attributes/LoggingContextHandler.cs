using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.logging.Attributes;

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
