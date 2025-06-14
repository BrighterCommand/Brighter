using Xunit;
using Paramore.Brighter.ServiceActivator.Ports;
using Paramore.Brighter.ServiceActivator.Ports.Commands;

namespace Paramore.Brighter.Core.Tests.ControlBus
{
    public class ConfigurationCommandToMessageMapperTests
    {
        private readonly IAmAMessageMapper<ConfigurationCommand> _mapper;
        private Message _message;
        private readonly ConfigurationCommand _command;
        private readonly Publication _publication;

        public ConfigurationCommandToMessageMapperTests()
        {
            _mapper = new ConfigurationCommandMessageMapper();

            _command = new ConfigurationCommand(ConfigurationCommandType.CM_STARTALL, new SubscriptionName("getallthethings"));
            
            _publication = new Publication { Topic = new RoutingKey("ConfigurationCommand") };
        }


        [Fact]
        public void When_mapping_to_a_wire_message_from_a_configuration_command()
        {
            _message = _mapper.MapToMessage(_command, _publication);

            // Should serialize the command type to the message body
            Assert.Contains("\"type\":\"CM_STARTALL", _message.Body.Value);
            // Should serialize the message type to the header
            Assert.Equal(MessageType.MT_COMMAND, _message.Header.MessageType);
            // Should serialize the connection name to the message body
            Assert.Contains("\"subscriptionName\":\"getallthethings\"", _message.Body.Value);
            // Should serialize the message id to the message body
            Assert.Contains($"\"id\":\"{_command.Id.Value}\"", _message.Body.Value);

        }
    }
}
