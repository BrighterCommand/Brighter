using System;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    sealed class MyValidationHandlerAsyncAttribute : RequestHandlerAttribute
    {
        public MyValidationHandlerAsyncAttribute (int step)
            : base(step)
        {
        }

        public override Type GetHandlerType()
        {
            return typeof(MyValidationHandlerAsync<>);
        }
    }
}
