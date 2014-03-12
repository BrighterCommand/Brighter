using System;
using paramore.commandprocessor.exceptionpolicy.Attributes;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.ExceptionPolicy.TestDoubles
{
   internal class MyFailsWithDivideByZeroHandler : RequestHandler<MyCommand>
    {
       private static bool receivedCommand= false;

        [UsePolicy(policy: "MyDivideByZeroPolicy", step: 1)]
        public override MyCommand Handle(MyCommand command)
        {
            receivedCommand = true;
            throw new DivideByZeroException();
        }

       public static bool ShouldRecieve(MyCommand myCommand)
       {
           return receivedCommand;
       }
    }
}
