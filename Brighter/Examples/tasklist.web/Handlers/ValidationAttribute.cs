using System;
using paramore.commandprocessor;

namespace tasklist.web.Handlers
{
    public class ValidationAttribute : RequestHandlerAttribute
    {
        public ValidationAttribute(int step, HandlerTiming timing)
            : base(step, timing)
        {}

        public override Type GetHandlerType()
        {
            return typeof(ValidationHandler<>);
        }
    }
}