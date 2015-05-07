using System.Reflection;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor;
using paramore.brighter.serviceactivator.Ports.Commands;

namespace paramore.brighter.serviceactivator.Ports.Mappers
{
    public class ConfigurationCommandMessageMapper : IAmAMessageMapper<ConfigurationCommand>
    {
        public Message MapToMessage(ConfigurationCommand request)
        {
            var topic = System.Environment.MachineName + Assembly.GetExecutingAssembly().GetName();

            var header = new MessageHeader(messageId: request.Id, topic: topic, messageType: MessageType.MT_EVENT);
            var body = new MessageBody(JsonConvert.SerializeObject(request));
            var message = new Message(header, body);
            return message;
        }

        public ConfigurationCommand MapToRequest(Message message)
        {
            return JsonConvert.DeserializeObject<ConfigurationCommand>(message.Body.Value);
        }

    }
}
