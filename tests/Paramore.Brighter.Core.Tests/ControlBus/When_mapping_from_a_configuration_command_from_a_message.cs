using Xunit;
using Paramore.Brighter.ServiceActivator.Ports;
using Paramore.Brighter.ServiceActivator.Ports.Commands;

namespace Paramore.Brighter.Core.Tests.ControlBus
{
    public class ConfigurationCommandMessageMapperTests 
    {
        private readonly IAmAMessageMapper<ConfigurationCommand> _mapper;
        private readonly Message _message;
        private ConfigurationCommand _command;

        public ConfigurationCommandMessageMapperTests()
        {
            _mapper = new ConfigurationCommandMessageMapper();

            var command = new ConfigurationCommand(ConfigurationCommandType.CM_STARTALL, "getallthethings");
            _message = _mapper.MapToMessage(command, new Publication(){Topic = new RoutingKey("myTopic")});
        }

        [Fact]
        public void When_mapping_from_a_configuration_command_from_a_message()
        {
            _command = _mapper.MapToRequest(_message);

            // Should rehydrate the command type
            Assert.Equal(ConfigurationCommandType.CM_STARTALL, _command.Type);
            // Should rehydrate the connection name
            Assert.Equal(new SubscriptionName("getallthethings"), _command.SubscriptionName);

        }
    }
}
