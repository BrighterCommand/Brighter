using System;
using System.Reflection;
using Newtonsoft.Json;
using Paramore.Brighter.ServiceActivator.Ports.Commands;

namespace Paramore.Brighter.ServiceActivator.Ports
{
    public class ConfigurationCommandMessageMapper : IAmAMessageMapper<ConfigurationCommand>
    {
        public Message MapToMessage(ConfigurationCommand request)
        {
            var topic = Environment.MachineName + Assembly.GetEntryAssembly().GetName();

            var header = new MessageHeader(messageId: request.Id, topic: topic, messageType: MessageType.MT_COMMAND);
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
