using Newtonsoft.Json;
using Paramore.Brighter.Monitoring.Events;

namespace Paramore.Brighter.Monitoring.Mappers
{
    public class MonitorEventMessageMapper : IAmAMessageMapper<MonitorEvent>
    {
        /// <summary>
        /// Maps to message.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>Message.</returns>
        public Message MapToMessage(MonitorEvent request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: "paramore.monitoring.event", messageType: MessageType.MT_COMMAND);
            var body = new MessageBody(JsonConvert.SerializeObject(request));
            var message = new Message(header, body);
            return message;
        }

        /// <summary>
        /// Maps to request.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>TRequest.</returns>
        public MonitorEvent MapToRequest(Message message)
        {
            return JsonConvert.DeserializeObject<MonitorEvent>(message.Body.Value);
        }
    }
}
