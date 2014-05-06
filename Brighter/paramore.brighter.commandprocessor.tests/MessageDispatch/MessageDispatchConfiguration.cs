using System.Collections.Generic;
using Common.Logging;
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.ServiceActivatorConfiguraton;

namespace paramore.commandprocessor.tests.MessageDispatch
{
    public class When_configuring_a_message_dispatcher
    {
        static ILog logger;
        static ConnectionElements connectionElements;
        static IEnumerable<Connection> connections;
        static IAdaptAnInversionOfControlContainer container;
        static Dispatcher dispatcher;

        Establish configuration = () =>
        {
            logger = A.Fake<ILog>();
            container = A.Fake<IAdaptAnInversionOfControlContainer>();

            var configuration = ServiceActivatorConfigurationSection.GetConfiguration();
            connectionElements = configuration.Connections;
            connections = ConnectionFactory.Create(connectionElements);
        };

        Because of = () => dispatcher = new Dispatcher(container, connections, logger);
    }
}
