using FakeItEasy;
using NUnit.Framework;

namespace Paramore.Brighter.Tests.ControlBus
{
    [TestFixture]
    public class ControlBusSenderFactoryTests
    {
        private IAmAControlBusSender s_sender;
        private IAmAControlBusSenderFactory s_senderFactory;
        private IAmAMessageStore<Message> s_fakeMessageStore;
        private IAmAMessageProducer s_fakeGateway;

        [SetUp]
        public void Establish()
        {
            s_fakeMessageStore = A.Fake<IAmAMessageStore<Message>>();
            s_fakeGateway = A.Fake<IAmAMessageProducer>();
 
            s_senderFactory = new ControlBusSenderFactory();
        }

        [Test]
        public void When_creating_a_control_bus_sender()
        {
            s_sender = s_senderFactory.Create(s_fakeMessageStore, s_fakeGateway);

            //_should_create_a_control_bus_sender
            Assert.NotNull(s_sender);
        }
    }
}
