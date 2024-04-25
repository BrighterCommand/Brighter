using System;
using System.Reflection;
using System.Text.Json;
using Paramore.Brighter.ServiceActivator.Ports.Commands;

namespace Paramore.Brighter.ServiceActivator.Ports
{
    public class ConfigurationCommandMessageMapper : IAmAMessageMapper<ConfigurationCommand>
    {
        public IRequestContext Context { get; set; }

        public Message MapToMessage(ConfigurationCommand request, Publication publication)
        {
            var topic = Environment.MachineName + Assembly.GetEntryAssembly()?.GetName();

            var header = new MessageHeader(messageId: request.Id, topic: topic, messageType: MessageType.MT_COMMAND);
            var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
            var message = new Message(header, body);
            return message;
        }

        public ConfigurationCommand MapToRequest(Message message)
        {
            return JsonSerializer.Deserialize<ConfigurationCommand>(message.Body.Value, JsonSerialisationOptions.Options);
        }

    }
}
