
using System.Threading.Tasks;
using paramore.commandprocessor.timeoutpolicy.Extensions;

namespace paramore.commandprocessor.timeoutpolicy.Handlers
{
    public class TimeoutPolicyHandler<TRequest> : RequestHandler<TRequest> where TRequest : class, IRequest
    {
        private int milliseconds;

        public override void InitializeFromAttributeParams(params object[] initializerList)
        {
            milliseconds = (int) initializerList[0];
        }

        public override TRequest Handle(TRequest command)
        {
            var task = Task.Factory.StartNew(() => base.Handle(command))
                .TimeoutAfter(millisecondsTimeout: milliseconds);

            task.Wait();

            return command;
        }
    }
}