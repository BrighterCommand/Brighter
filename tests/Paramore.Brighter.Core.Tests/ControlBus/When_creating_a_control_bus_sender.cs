using System;
using System.Collections.Generic;
using System.Transactions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.ControlBus
{
    public class ControlBusSenderFactoryTests
    {
        private IAmAControlBusSender? _sender;
        private readonly IAmAControlBusSenderFactory _senderFactory;
        private readonly IAmAnOutboxSync<Message, CommittableTransaction> _outbox;
        private readonly IAmAMessageProducerSync _gateway;
        public ControlBusSenderFactoryTests()
        {
            _outbox = new InMemoryOutbox(TimeProvider.System);
            _gateway = new InMemoryMessageProducer(new InternalBus());
            _senderFactory = new ControlBusSenderFactory();
        }

        [Test]
        public async Task When_creating_a_control_bus_sender()
        {
            _sender = _senderFactory.Create<Message, CommittableTransaction>(_outbox, new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { new RoutingKey("MyTopic"), _gateway }, }), tracer: new BrighterTracer());
            await Assert.That(_sender).IsNotNull();
        }
    }
}