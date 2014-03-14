using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.timeoutpolicy.Attributes;

namespace paramore.commandprocessor.tests.Timeout.TestDoubles
{
    internal class MyPassesTimeoutHandler : RequestHandler<MyCommand>
    {
        public static bool CommandRecieved { get; set; }

        [TimeoutPolicy(milliseconds: 1000, step: 1)]
        public override MyCommand Handle(MyCommand command)
        {
            CommandRecieved = true;
            return base.Handle(command);
        }

        public static bool ShouldRecieve(MyCommand command)
        {
            return CommandRecieved;
        }
    }
}