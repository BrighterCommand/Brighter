using FakeItEasy;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.Ports.Commands;
using paramore.brighter.serviceactivator.Ports.Handlers;

namespace paramore.brighter.commandprocessor.tests.nunit.ControlBus
{
    public class When_receiving_a_stop_message_for_a_connection : NUnit.Specifications.ContextSpecification
    {
        const string CONNECTION_NAME = "Test";
        private static ConfigurationCommandHandler s_configurationCommandHandler;
        private static ConfigurationCommand s_configurationCommand;
        private static IDispatcher s_dispatcher;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_dispatcher = A.Fake<IDispatcher>();

            s_configurationCommandHandler = new ConfigurationCommandHandler(s_dispatcher, logger);

            s_configurationCommand = new ConfigurationCommand(ConfigurationCommandType.CM_STOPCHANNEL) {ConnectionName = CONNECTION_NAME};
        };

        private Because _of = () => s_configurationCommandHandler.Handle(s_configurationCommand);

        private It _should_call_stop_for_the_given_connection = () => A.CallTo(() => s_dispatcher.Shut(CONNECTION_NAME));
    }
}