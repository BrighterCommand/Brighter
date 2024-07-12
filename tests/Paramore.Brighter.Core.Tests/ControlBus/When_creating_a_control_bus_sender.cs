using System;
using System.Collections.Generic;
using System.Transactions;
using FluentAssertions;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.ControlBus
{
    [Collection("CommandProcessor")]
    public class ControlBusSenderFactoryTests : IDisposable
    {
        private IAmAControlBusSender _sender;
        private readonly IAmAControlBusSenderFactory _senderFactory;
        private readonly IAmAnOutboxSync<Message, CommittableTransaction> _outbox;
        private readonly IAmAMessageProducerSync _gateway;

        public ControlBusSenderFactoryTests()
        {
            _outbox = new InMemoryOutbox(TimeProvider.System);
            _gateway = new InMemoryProducer(new InternalBus(), TimeProvider.System);
 
            _senderFactory = new ControlBusSenderFactory();
        }

        [Fact]
        public void When_creating_a_control_bus_sender()
        {
            _sender = _senderFactory.Create<Message, CommittableTransaction>(
                _outbox, 
                new ProducerRegistry(new Dictionary<string, IAmAMessageProducer> {{"MyTopic", _gateway},}),
                tracer: new BrighterTracer());

            _sender.Should().NotBeNull();
        }
        
        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
