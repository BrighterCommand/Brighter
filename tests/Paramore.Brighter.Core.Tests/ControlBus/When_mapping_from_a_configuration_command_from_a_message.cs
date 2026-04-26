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
            _message = _mapper.MapToMessage(command, new Publication() { Topic = new RoutingKey("myTopic") });
        }

        [Test]
        public async Task When_mapping_from_a_configuration_command_from_a_message()
        {
            _command = _mapper.MapToRequest(_message);
            // Should rehydrate the command type
            await Assert.That(_command.Type).IsEqualTo(ConfigurationCommandType.CM_STARTALL);
            // Should rehydrate the connection name
            await Assert.That(_command.SubscriptionName).IsEqualTo(new SubscriptionName("getallthethings"));
        }
    }
}