using FakeItEasy;
using FluentAssertions;
using Xunit;

namespace Paramore.Brighter.Tests.ControlBus
{
    public class ControlBusSenderFactoryTests
    {
        private IAmAControlBusSender s_sender;
        private readonly IAmAControlBusSenderFactory s_senderFactory;
        private readonly IAmAnOutbox<Message> s_fakeOutbox;
        private readonly IAmAMessageProducer s_fakeGateway;

        public ControlBusSenderFactoryTests()
        {
            s_fakeOutbox = A.Fake<IAmAnOutbox<Message>>();
            s_fakeGateway = A.Fake<IAmAMessageProducer>();
 
            s_senderFactory = new ControlBusSenderFactory();
        }

        [Fact]
        public void When_creating_a_control_bus_sender()
        {
            s_sender = s_senderFactory.Create(s_fakeOutbox, s_fakeGateway);

            //_should_create_a_control_bus_sender
            s_sender.Should().NotBeNull();
        }
    }
}
