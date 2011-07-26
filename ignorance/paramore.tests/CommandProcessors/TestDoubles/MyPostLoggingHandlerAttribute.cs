using System;
using Paramore.Services.CommandHandlers;
using Paramore.Services.CommandProcessors;

namespace Paramore.Tests.CommandProcessors.TestDoubles
{
    internal class MyPostLoggingHandlerAttribute : RequestHandlerAttribute
    {
        public MyPostLoggingHandlerAttribute(int step, HandlerTiming timing)
            : base(step, timing)
        {
        }

        public override Type GetHandlerType()
        {
            return typeof(MyLoggingHander<>);
        }
    }
}