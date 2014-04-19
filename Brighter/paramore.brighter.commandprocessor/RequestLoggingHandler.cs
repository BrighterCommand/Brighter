using System;
using Common.Logging;
using Newtonsoft.Json;

namespace paramore.brighter.commandprocessor
{
    public class RequestLoggingHandler<TRequest> : RequestHandler<TRequest> where TRequest : class, IRequest
    {
        private HandlerTiming timing;

        public RequestLoggingHandler(ILog logger)
            :base(logger)
        {}

        public override void InitializeFromAttributeParams(params object[] initializerList)
        {
            timing = (HandlerTiming)initializerList[0];
        }

        public override TRequest Handle(TRequest command)
        {
            LogCommand(command);
            return base.Handle(command);
        }
        
        private void LogCommand(TRequest request)
        {
            logger.Info(m => m("Logging handler pipeline call. Pipeline timing {0} target, for {1} with values of {2} at: {3}", timing.ToString(), typeof (TRequest), JsonConvert.SerializeObject(request),DateTime.UtcNow));
        }
    }
}
