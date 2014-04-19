using System;
using Common.Logging;

namespace paramore.brighter.commandprocessor
{
    public class RequestLoggingHandler<TRequest> : RequestHandler<TRequest> where TRequest : class, IRequest
    {
        private readonly ILog logger;

        public RequestLoggingHandler(ILog logger)
        {
            this.logger = logger;
        }

        public override TRequest Handle(TRequest command)
        {
            LogCommand(command);
            return base.Handle(command);
        }
        
        private void LogCommand(TRequest request)
        {
            logger.Info(m => m("Logging request type {0} with request id {1) at {2}", request.GetType().ToString(), request.Id, DateTime.UtcNow));
        }
    }
}
