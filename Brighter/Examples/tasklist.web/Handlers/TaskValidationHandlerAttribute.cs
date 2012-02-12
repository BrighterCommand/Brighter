using System;
using paramore.commandprocessor;

namespace tasklist.web.Handlers
{
    public class TaskValidationHandlerAttribute : RequestHandlerAttribute
    {
        public TaskValidationHandlerAttribute(int step, HandlerTiming timing)
            : base(step, timing)
        {
        }

        public override Type GetHandlerType()
        {
            return typeof(TaskValidationHandler);
        }
    }
}