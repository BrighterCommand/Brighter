using System.Collections.Generic;
using FakeItEasy;
using FluentAssertions;
using Xunit;

namespace Paramore.Brighter.Core.Tests.ControlBus
{
    public class ControlBusSenderFactoryTests
    {
        private IAmAControlBusSender s_sender;
        private readonly IAmAControlBusSenderFactory s_senderFactory;
        private readonly IAmAnOutboxSync<Message> _fakeOutboxSync;
        private readonly IAmAMessageProducerSync s_fakeGateway;

        public ControlBusSenderFactoryTests()
        {
            _fakeOutboxSync = A.Fake<IAmAnOutboxSync<Message>>();
            s_fakeGateway = A.Fake<IAmAMessageProducerSync>();
 
            s_senderFactory = new ControlBusSenderFactory();
        }

        [Fact]
        public void When_creating_a_control_bus_sender()
        {
            s_sender = s_senderFactory.Create(
                _fakeOutboxSync, 
                new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>() {{"MyTopic", s_fakeGateway},}));

            //_should_create_a_control_bus_sender
            s_sender.Should().NotBeNull();
        }
    }
}
