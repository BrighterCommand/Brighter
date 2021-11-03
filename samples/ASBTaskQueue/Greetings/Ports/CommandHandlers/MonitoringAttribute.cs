using System;
using Paramore.Brighter;

namespace Greetings.Ports.CommandHandlers
{
    public class MonitoringAttribute : RequestHandlerAttribute
    {
        public MonitoringAttribute(int step, HandlerTiming timing)
            : base(step, timing)
        { }

        public override object[] InitializerParams()
        {
            return new object[] { Timing };
        }

        public override Type GetHandlerType()
        {
            return typeof(MonitoringAsyncHandler<>);
        }
    }
}