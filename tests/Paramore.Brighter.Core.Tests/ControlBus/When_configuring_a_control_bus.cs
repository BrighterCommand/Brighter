using System;
using FakeItEasy;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.ControlBus;
using Paramore.Brighter.ServiceActivator.Ports.Commands;

namespace Paramore.Brighter.Core.Tests.ControlBus
{
    public class ControlBusBuilderTests
    {
        private Dispatcher _controlBus;
        private readonly ControlBusReceiverBuilder _busReceiverBuilder;
        private readonly string _hostName = "tests";
        public ControlBusBuilderTests()
        {
            var dispatcher = A.Fake<IDispatcher>();
            var bus = new InternalBus();
            _busReceiverBuilder = (ControlBusReceiverBuilder.With().Dispatcher(dispatcher).ProducerRegistryFactory(new InMemoryProducerRegistryFactory(bus, [new Publication { Topic = new RoutingKey("MyTopic"), RequestType = typeof(ConfigurationCommand) }], InstrumentationOptions.All)).ChannelFactory(new InMemoryChannelFactory(bus, TimeProvider.System)) as ControlBusReceiverBuilder)!;
        }

        [Test]
        public async Task When_configuring_a_control_bus()
        {
            _controlBus = _busReceiverBuilder.Build(_hostName);
            await Assert.That((_controlBus.Subscriptions).Any(cn => cn.Name == $"{_hostName}.{ControlBusReceiverBuilder.CONFIGURATION}")).IsTrue();
            await Assert.That((_controlBus.Subscriptions).Any(cn => cn.Name == $"{_hostName}.{ControlBusReceiverBuilder.HEARTBEAT}")).IsTrue();
            await Assert.That(_controlBus.CommandProcessor).IsNotNull();
        }
    }
}
