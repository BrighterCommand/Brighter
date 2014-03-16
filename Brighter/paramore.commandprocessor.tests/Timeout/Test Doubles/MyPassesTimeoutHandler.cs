using System.Threading;
using System.Threading.Tasks;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.timeoutpolicy.Attributes;
using paramore.commandprocessor.timeoutpolicy.Handlers;

namespace paramore.commandprocessor.tests.Timeout.TestDoubles
{
    internal class MyPassesTimeoutHandler : RequestHandler<MyCommand>
    {
        public static bool CommandRecieved { get; set; }

        [TimeoutPolicy(milliseconds: 1000, step: 1)]
        public override MyCommand Handle(MyCommand command)
        {
            var ct = (CancellationToken) Context.Bag[TimeoutPolicyHandler<MyCommand>.CONTEXT_BAG_TIMEOUT_CANCELLATION_TOKEN];
            Task.Delay(100, ct).ContinueWith((antecedent) =>
            {
                CommandRecieved = true;
            }).Wait();
            return base.Handle(command);

        }

        public static bool ShouldRecieve(MyCommand command)
        {
            return CommandRecieved;
        }
    }
}