using System;
using Paramore.Services.CommandProcessors;

namespace Paramore.Tests.services.CommandProcessors.TestDoubles
{
    internal class MyLoggingHandlerAttribute : RequestHandlerAttribute
    {
        public MyLoggingHandlerAttribute(int step)
            : base(step)
        {
        }

        public override Type GetHandlerType()
        {
            return typeof(MyLoggingHander<>);
        }
    }
}