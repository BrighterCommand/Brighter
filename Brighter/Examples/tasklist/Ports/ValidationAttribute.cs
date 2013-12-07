using System;
using Tasklist.Ports.Handlers;
using paramore.commandprocessor;

namespace Tasklist.Ports
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