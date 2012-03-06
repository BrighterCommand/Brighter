using System;
using paramore.commandprocessor;

namespace tasklist.web.Handlers
{
    public class BeginTransactionAttribute : RequestHandlerAttribute
    {
        public BeginTransactionAttribute(int step, HandlerTiming timing)
            : base(step, timing)
        {}

        public override Type GetHandlerType()
        {
            return typeof (BeginTransaction<>);
        }
    }
}