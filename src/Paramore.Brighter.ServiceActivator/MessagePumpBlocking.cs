using System.Threading;
using Microsoft.Extensions.Logging;

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

        protected override void DispatchRequest(MessageHeader messageHeader, TRequest request)
        {
            s_logger.LogDebug("MessagePump: Dispatching message {Id} from {ChannelName} on thread # {ManagementThreadId}", request.Id, Thread.CurrentThread.ManagedThreadId, Channel.Name);

            var messageType = messageHeader.MessageType;

            ValidateMessageType(messageType, request);

            switch (messageType)
            {
                case MessageType.MT_COMMAND:
                {
                    CommandProcessor.Send(request);
                    break;
                }
                case MessageType.MT_DOCUMENT:
                case MessageType.MT_EVENT:
                {
                    CommandProcessor.Publish(request);
                    break;
                }
            }
        }
    }
}
