using System;
using System.Collections.Generic;
using System.Transactions;
using FakeItEasy;
using FluentAssertions;
using Xunit;

namespace Paramore.Brighter.Core.Tests.ControlBus
{
    [Collection("CommandProcessor")]
    public class ControlBusSenderFactoryTests : IDisposable
    {
        private IAmAControlBusSender _sender;
        private readonly IAmAControlBusSenderFactory _senderFactory;
        private readonly IAmAnOutboxSync<Message, CommittableTransaction> _fakeOutbox;
        private readonly IAmAMessageProducerSync _fakeGateway;

        public ControlBusSenderFactoryTests()
        {
            _fakeOutbox = A.Fake<IAmAnOutboxSync<Message, CommittableTransaction>>();
            _fakeGateway = A.Fake<IAmAMessageProducerSync>();
 
            _senderFactory = new ControlBusSenderFactory();
        }

        [Fact]
        public void When_creating_a_control_bus_sender()
        {
            _sender = _senderFactory.Create<Message, CommittableTransaction>(
                _fakeOutbox, 
                new ProducerRegistry(new Dictionary<string, IAmAMessageProducer> {{"MyTopic", _fakeGateway},}));

            //_should_create_a_control_bus_sender
            _sender.Should().NotBeNull();
        }
        
        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
