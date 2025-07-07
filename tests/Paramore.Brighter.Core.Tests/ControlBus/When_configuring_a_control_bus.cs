using System;
using FakeItEasy;
using Paramore.Brighter.Observability;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.ControlBus;
using Paramore.Brighter.ServiceActivator.Ports.Commands;

namespace Paramore.Brighter.Core.Tests.ControlBus
{
    [Collection("CommandProcessor")]
    public class ControlBusBuilderTests : IDisposable
    {
        private Dispatcher _controlBus;
        private readonly ControlBusReceiverBuilder _busReceiverBuilder;
        private readonly string _hostName = "tests";

        public ControlBusBuilderTests()
        {
            var dispatcher = A.Fake<IDispatcher>();
            var bus = new InternalBus();

            _busReceiverBuilder = (ControlBusReceiverBuilder
                .With()
                .Dispatcher(dispatcher)
                .ProducerRegistryFactory(new InMemoryProducerRegistryFactory(bus, new []
                {
                    new Publication{Topic = new RoutingKey("MyTopic"), RequestType = typeof(ConfigurationCommand)}
                }, InstrumentationOptions.All))
                .ChannelFactory(new InMemoryChannelFactory(bus, TimeProvider.System)) as ControlBusReceiverBuilder)!;
        }

        [Fact]
        public void When_configuring_a_control_bus()
        {
            _controlBus = _busReceiverBuilder.Build(_hostName);

            Assert.Contains(_controlBus.Subscriptions, cn => cn.Name == $"{_hostName}.{ControlBusReceiverBuilder.CONFIGURATION}");
            Assert.Contains(_controlBus.Subscriptions, cn => cn.Name == $"{_hostName}.{ControlBusReceiverBuilder.HEARTBEAT}");
            Assert.NotNull(_controlBus.CommandProcessor);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
