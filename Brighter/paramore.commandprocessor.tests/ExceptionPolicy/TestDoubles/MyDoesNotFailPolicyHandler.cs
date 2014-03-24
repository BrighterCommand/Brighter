using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.exceptionpolicy.Attributes;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.ExceptionPolicy.TestDoubles
{
    internal class MyDoesNotFailPolicyHandler : RequestHandler<MyCommand>
    {
        public static bool ReceivedCommand { get; set; }

        static MyDoesNotFailPolicyHandler()
        {
            ReceivedCommand = false;
        }

        [UsePolicy(policy: "MyDivideByZeroPolicy", step: 1)]
        public override MyCommand Handle(MyCommand command)
        {
            ReceivedCommand = true;
            return base.Handle(command);
        }

       public static bool ShouldRecieve(MyCommand myCommand)
       {
           return ReceivedCommand;
       }
    }
}