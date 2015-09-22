using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;

namespace GenericListener.Ports.Attributes
{
    public class LoggingContextHandler<TRequest> : RequestHandler<TRequest> where TRequest : class, IRequest
    {
        public LoggingContextHandler()
            : base(LogProvider.GetCurrentClassLogger())
        {
        }

        [RequestLogging(0, HandlerTiming.Before)]
        public override TRequest Handle(TRequest command)
        {
            var result = base.Handle(command);
            return result;
        }
    }
}
