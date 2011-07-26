using System;
using Paramore.Services.CommandProcessors;

namespace Paramore.Tests.CommandProcessors.TestDoubles
{
    internal class MyValidationHandlerAttribute : RequestHandlerAttribute
    {
        public MyValidationHandlerAttribute(int step)
            : base(step)
        {
        }

        public override Type GetHandlerType()
        {
            return typeof(MyValidationHandler<>);
        }
    }
}