using System.Text.Json;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Monitoring.Events;

namespace Paramore.Brighter.Monitoring.Mappers
{
    public class MonitorEventMessageMapper : IAmAMessageMapper<MonitorEvent>
    {
        public IRequestContext? Context { get; set; }

        /// <summary>
        /// Maps to message.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="publication">The publication with metadata</param>
        /// <returns>Message.</returns>
        public Message MapToMessage(MonitorEvent request, Publication publication)
        {
            var header = new MessageHeader(messageId: request.Id, topic: new RoutingKey(publication.Topic ?? ""), messageType: request.RequestToMessageType());
            var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
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
            return JsonSerializer.Deserialize<MonitorEvent>(message.Body.Value, JsonSerialisationOptions.Options)!;
        }
    }
}
