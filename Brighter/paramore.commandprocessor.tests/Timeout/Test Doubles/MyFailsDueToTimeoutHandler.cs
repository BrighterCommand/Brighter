using System.Threading.Tasks;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.timeoutpolicy.Attributes;

namespace paramore.commandprocessor.tests.Timeout.TestDoubles
{
    internal class MyFailsDueToTimeoutHandler: RequestHandler<MyCommand>
    {
        [TimeoutPolicy(milliseconds: 1000, step: 1)]
        public override MyCommand Handle(MyCommand command)
        {
            var task = Task.Delay(2000);
            task.Wait(3000);
            return base.Handle(command);
        }
    }
}