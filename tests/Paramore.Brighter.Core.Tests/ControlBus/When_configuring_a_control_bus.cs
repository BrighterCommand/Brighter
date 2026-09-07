using System;
using FakeItEasy;
using Paramore.Brighter.Observability;
using Xunit;
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

            _busReceiverBuilder = (ControlBusReceiverBuilder
                .With(Initializer.TestLoggerFactory)
                .Dispatcher(dispatcher)
                .ProducerRegistryFactory(new InMemoryProducerRegistryFactory(bus,
                [
                    new Publication{Topic = new RoutingKey("MyTopic"), RequestType = typeof(ConfigurationCommand)}
                ], Initializer.TestLoggerFactory, InstrumentationOptions.All))
                .ChannelFactory(new InMemoryChannelFactory(bus, TimeProvider.System, loggerFactory: Initializer.TestLoggerFactory)) as ControlBusReceiverBuilder)!;
        }

        [Fact]
        public void When_configuring_a_control_bus()
        {
            _controlBus = _busReceiverBuilder.Build(_hostName);

            Assert.Contains(_controlBus.Subscriptions, cn => cn.Name == $"{_hostName}.{ControlBusReceiverBuilder.CONFIGURATION}");
            Assert.Contains(_controlBus.Subscriptions, cn => cn.Name == $"{_hostName}.{ControlBusReceiverBuilder.HEARTBEAT}");
            Assert.NotNull(_controlBus.CommandProcessor);
        }
    }
}
