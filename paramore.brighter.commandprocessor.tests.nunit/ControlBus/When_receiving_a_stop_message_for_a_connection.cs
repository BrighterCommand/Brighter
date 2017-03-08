using FakeItEasy;
using NUnit.Framework;
using paramore.brighter.serviceactivator;

namespace paramore.brighter.commandprocessor.tests.nunit.ControlBus
{
    [TestFixture]
    public class ConfigurationCommandStopTests
    {
        const string CONNECTION_NAME = "Test";
        private ConfigurationCommandHandler _configurationCommandHandler;
        private ConfigurationCommand _configurationCommand;
        private IDispatcher _dispatcher;

        [SetUp]
        public void Establish()
        {
            _dispatcher = A.Fake<IDispatcher>();
            _configurationCommandHandler = new ConfigurationCommandHandler(_dispatcher);
            _configurationCommand = new ConfigurationCommand(ConfigurationCommandType.CM_STOPCHANNEL) {ConnectionName = CONNECTION_NAME};
        }

        [Test]
        public void When_receiving_a_stop_message_for_a_connection()
        {
            _configurationCommandHandler.Handle(_configurationCommand);

            //_should_call_stop_for_the_given_connection
            A.CallTo(() => _dispatcher.Shut(CONNECTION_NAME));
        }
    }
}