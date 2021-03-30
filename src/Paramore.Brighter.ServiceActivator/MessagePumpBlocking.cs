using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Used when the message pump should block for I/O
    /// Will guarantee strict ordering of the messages on the queue
    /// Predictable performance as only one thread, allows you to configure number of performers for number of threads to use
    /// Lower throughput than asuync
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    public class MessagePumpBlocking<TRequest> : MessagePump<TRequest> where TRequest : class, IRequest
    {
        public MessagePumpBlocking(
            IAmACommandProcessor commandProcessor, 
            IAmAMessageMapper<TRequest> messageMapper) 
            : base(commandProcessor, messageMapper)
        {
        }

        public override Task Run()
        {
            var tcs = new TaskCompletionSource<object>();
            //Nothing will be awaited in this path, so not async
            RunImpl().GetAwaiter().GetResult();
            tcs.SetResult(new object());
            return tcs.Task;
        }

        protected override Task DispatchRequest(MessageHeader messageHeader, TRequest request)
        {
            var tcs = new TaskCompletionSource<object>();

            _logger.Value.DebugFormat("MessagePump: Dispatching message {0} from {2} on thread # {1}", request.Id, Thread.CurrentThread.ManagedThreadId, Channel.Name);

            var messageType = messageHeader.MessageType;

            ValidateMessageType(messageType, request);

            switch (messageType)
            {
                case MessageType.MT_COMMAND:
                {
                    _commandProcessor.Send(request);
                    break;
                }
                case MessageType.MT_DOCUMENT:
                case MessageType.MT_EVENT:
                {
                    _commandProcessor.Publish(request);
                    break;
                }
            }

            tcs.SetResult(new object());
            return tcs.Task;
        }
    }
}
