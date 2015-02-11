using Machine.Specifications;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.controlbus.Ports.Commands;
using paramore.brighter.serviceactivator.controlbus.Ports.Handlers;

namespace paramore.commandprocessor.tests.ControlBus
{
    public class When_receiving_an_all_stop_message
    {
        static ConfigurationMessageHandler configurationMessageHandler;
        static ConfigurationCommand configurationCommand;
        static IDispatcher dispatcher;

        Establish context = () =>
        {
            configurationCommand = new ConfigurationCommand(ConfigurationCommandType.CM_STOPALL);
        }; 

        Because of = () => configurationMessageHandler.Handle(configurationCommand);
    }
}
