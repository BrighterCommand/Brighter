using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.ServiceActivator.Logging;

namespace Paramore.Brighter.ServiceActivator
{
    internal class MessagePumpAsync<TRequest> : MessagePumpBase<TRequest>, IAmAMessagePump where TRequest : class, IRequest
    {
        public MessagePumpAsync(IAmACommandProcessor commandProcessor, IAmAMessageMapper<TRequest> messageMapper)
            : base(commandProcessor, messageMapper)
        {}

        public bool IsAsync => true;

        protected override async Task DispatchRequest(MessageHeader messageHeader, TRequest request)
        {
            _logger.Value.DebugFormat("MessagePump: Dispatching message {0} from {2} on thread # {1}", request.Id, Thread.CurrentThread.ManagedThreadId, Channel.Name);

            if (messageHeader.MessageType == MessageType.MT_COMMAND && request is IEvent)
            {
                throw new ConfigurationException(string.Format("Message {0} mismatch. Message type is '{1}' yet mapper produced message of type IEvent", request.Id, MessageType.MT_COMMAND));
            }
            if (messageHeader.MessageType == MessageType.MT_EVENT && request is ICommand)
            {
                throw new ConfigurationException(string.Format("Message {0} mismatch. Message type is '{1}' yet mapper produced message of type ICommand", request.Id, MessageType.MT_EVENT));
            }

            switch (messageHeader.MessageType)
            {
                case MessageType.MT_COMMAND:
                    {
                        await _commandProcessor.SendAsync(request, continueOnCapturedContext: true);
                        break;
                    }
                case MessageType.MT_DOCUMENT:
                case MessageType.MT_EVENT:
                    {
                        await _commandProcessor.PublishAsync(request, continueOnCapturedContext: true);
                        break;
                    }
            }
        }

        protected override void SynchronizationContextHook()
        {
            //we take control of the synchonization context to provide a reactor model i.e. uses one thread, does not use thread pool for callbacks
            //TODO: We want to support a Single Threaded Apartment, but it needs work
            //SynchronizationContext.SetSynchronizationContext(new MessagePumpSynchronizationContext(Channel));
        }
    }
}
