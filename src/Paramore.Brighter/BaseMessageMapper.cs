using System;

namespace Paramore.Brighter
{
    public abstract class BaseMessageMapper<T> : IAmAMessageMapper<T> where T : class, IRequest
    {
        private readonly IRequestContext _requestContext;
        private readonly Func<T, string> _routingAction;
        private readonly RoutingKey _routingKey;

        protected BaseMessageMapper(IRequestContext requestContext, RoutingKey routingKey = null,  Func<T, string> routingKeyFunc = null)
        {
            _requestContext = requestContext;
            _routingKey = routingKey;
            _routingAction = routingKeyFunc;
        }

        public Message MapToMessage(T request)
        {
            MessageType messageType = request switch
            {
                Command _ => MessageType.MT_COMMAND,
                Event _ => MessageType.MT_EVENT,
                _ => throw new ArgumentException("This message mapper can only map Commands and Events", nameof(request))
            };

            var topic = _routingAction?.Invoke(request) ?? _routingKey ?? request.GetType().Name;

            var messageHeader = new MessageHeader(request.Id, topic, messageType, _requestContext.Header.CorrelationId, contentType: "application/json");

            return new Message(messageHeader, CreateMessageBody(request));
        }

        protected abstract MessageBody CreateMessageBody(T request);

        public T MapToRequest(Message message)
        {
            _requestContext.Header.CorrelationId = message.Header.CorrelationId;

            return CreateType(message);
        }

        protected abstract T CreateType(Message message);
    }
}
