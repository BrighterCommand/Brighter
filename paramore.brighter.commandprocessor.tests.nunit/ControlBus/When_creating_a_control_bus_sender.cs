using FakeItEasy;
using nUnitShouldAdapter;
using NUnit.Specifications;

namespace paramore.brighter.commandprocessor.tests.nunit.ControlBus
{
    public class When_creating_a_control_bus_sender : ContextSpecification
    {
        private static IAmAControlBusSender s_sender;
        private static IAmAControlBusSenderFactory s_senderFactory;
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
