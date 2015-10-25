﻿using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.commandprocessor.tests.ControlBus
{
    public class When_creating_a_control_bus_sender
    {
        private static IAmAControlBusSender s_sender;
        private static IAmAControlBusSenderFactory s_senderFactory;
        private static MessagingConfiguration s_configuration;
        private static ILog s_logger;
        private static IAmAMessageStore<Message> s_fakeMessageStore;
        private static IAmAMessageProducer s_fakeGateway;

        private Establish _context = () =>
        {
            s_fakeMessageStore = A.Fake<IAmAMessageStore<Message>>();
            s_fakeGateway = A.Fake<IAmAMessageProducer>();
 
            s_senderFactory = new ControlBusSenderFactory();
        };

        private Because _of = () => s_sender = s_senderFactory.Create(s_fakeMessageStore, s_fakeGateway);

        private It _should_create_a_control_bus_sender = () => s_sender.ShouldNotBeNull();
    }
}
