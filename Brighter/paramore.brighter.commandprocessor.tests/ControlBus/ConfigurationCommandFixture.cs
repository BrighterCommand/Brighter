using Machine.Specifications;
using paramore.brighter.serviceactivator.controlbus.Ports.Commands;
using paramore.brighter.serviceactivator.controlbus.Ports.Handlers;

namespace paramore.commandprocessor.tests.ControlBus
{
    public class When_receiving_an_all_stop_message
    {
        static ConfigurationMessageHandler configurationMessageHandler;
        static ConfigurationMessage configurationMessage;

        Establish context = () =>
        {
            configurationMessage = new ConfigurationMessage(ConfigurationMessageType.CM_STOPALL);
        }; 

        Because of = () => configurationMessageHandler.Handle(configurationMessage);
    }
}
