using System;
using Newtonsoft.Json;
using Tasklist.Ports.Commands;
using paramore.brighter.commandprocessor;

namespace Tasklist.Ports.Handlers
{
    public class TraceHandler<TRequest> : RequestHandler<TRequest>
        where TRequest: class, IRequest, ICanBeValidated 
    {
        private readonly ITraceOutput traceOutput;

        public TraceHandler(ITraceOutput traceOutput)
        {
            this.traceOutput = traceOutput;
        }

        public override TRequest Handle(TRequest command)
        {
            traceOutput.WriteLine(string.Format("Calling handler for {0} with values of {1} at: {2}", typeof (TRequest), JsonConvert.SerializeObject(command), DateTime.UtcNow));

            base.Handle(command);

            traceOutput.WriteLine(string.Format("Finished calling handler for {0} at: {1}", typeof (TRequest), DateTime.UtcNow));

            return command;
        }
    }
}