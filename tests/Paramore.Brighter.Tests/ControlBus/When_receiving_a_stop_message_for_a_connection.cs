using FakeItEasy;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Ports.Commands;
using Paramore.Brighter.ServiceActivator.Ports.Handlers;

namespace Paramore.Brighter.Tests.ControlBus
{
    public class ConfigurationCommandStopTests
    {
        const string CONNECTION_NAME = "Test";
        private readonly ConfigurationCommandHandler _configurationCommandHandler;
        private readonly ConfigurationCommand _configurationCommand;
        private readonly IDispatcher _dispatcher;

        public ConfigurationCommandStopTests()
        {
            _dispatcher = A.Fake<IDispatcher>();
            _configurationCommandHandler = new ConfigurationCommandHandler(_dispatcher);
            _configurationCommand = new ConfigurationCommand(ConfigurationCommandType.CM_STOPCHANNEL) {ConnectionName = CONNECTION_NAME};
        }

        [Fact]
        public void When_receiving_a_stop_message_for_a_connection()
        {
            _configurationCommandHandler.Handle(_configurationCommand);

            //_should_call_stop_for_the_given_connection
            A.CallTo(() => _dispatcher.Shut(CONNECTION_NAME));
        }
    }
}