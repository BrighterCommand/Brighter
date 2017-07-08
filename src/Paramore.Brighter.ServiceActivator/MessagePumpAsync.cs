using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.ServiceActivator.Logging;

namespace Paramore.Brighter.ServiceActivator
{
    public class MessagePumpAsync<TRequest> : MessagePumpBase<TRequest>, IAmAMessagePump where TRequest : class, IRequest
    {
        public MessagePumpAsync(IAmAChannel channel, IAmACommandProcessor commandProcessor, IAmAMessageMapper<TRequest> messageMapper)
            : base(channel, commandProcessor, messageMapper)
        {}

        protected override async Task DispatchRequest(MessageHeader messageHeader, TRequest request, CancellationToken cancellationToken)
        {
            _logger.Value.DebugFormat("MessagePump: Dispatching message {0} from {1} on thread # {2}", request.Id, _channel.Name, Thread.CurrentThread.ManagedThreadId);

            if (messageHeader.MessageType == MessageType.MT_COMMAND && request is IEvent)
                throw new ConfigurationException($"Message {request.Id} mismatch. Message type is '{MessageType.MT_COMMAND}' yet mapper produced message of type {nameof(IEvent)}");

            if (messageHeader.MessageType == MessageType.MT_EVENT && request is ICommand)
                throw new ConfigurationException($"Message {request.Id} mismatch. Message type is '{MessageType.MT_EVENT}' yet mapper produced message of type {nameof(ICommand)}");

            switch (messageHeader.MessageType)
            {
                case MessageType.MT_COMMAND:
                    await _commandProcessor.SendAsync(request, true, cancellationToken).ConfigureAwait(true);
                    break;
                case MessageType.MT_DOCUMENT:
                case MessageType.MT_EVENT:
                    await _commandProcessor.PublishAsync(request, true, cancellationToken).ConfigureAwait(true);
                    break;
            }
        }
    }
}
