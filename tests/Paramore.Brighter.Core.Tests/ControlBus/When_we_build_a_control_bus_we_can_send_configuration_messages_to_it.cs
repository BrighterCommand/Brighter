using System;
using System.Reflection;
using FakeItEasy;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Observability;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.ControlBus;
using Paramore.Brighter.ServiceActivator.Ports.Commands;

namespace Paramore.Brighter.Core.Tests.ControlBus
{
    [Collection("CommandProcessor")]
    public class ControlBusTests : IDisposable
    {
        private readonly IDispatcher _dispatcher;
        private readonly Dispatcher _controlBus;
        private readonly ConfigurationCommand _configurationCommand;
        private Exception _exception;

        public ControlBusTests()
        {
            var topic =  new RoutingKey(Environment.MachineName + Assembly.GetEntryAssembly()?.GetName());
            _dispatcher = A.Fake<IDispatcher>();
            var bus = new InternalBus();

            ControlBusReceiverBuilder busReceiverBuilder = (ControlBusReceiverBuilder) ControlBusReceiverBuilder
                .With()
                .Dispatcher(_dispatcher)
                .ProducerRegistryFactory(new InMemoryProducerRegistryFactory(
                    bus,
                    [
                        new Publication{Topic = topic, RequestType = typeof(ConfigurationCommand)}
                    ], InstrumentationOptions.All))
                .ChannelFactory(new InMemoryChannelFactory(bus, TimeProvider.System));

            _controlBus = busReceiverBuilder.Build("tests");

            _configurationCommand = new ConfigurationCommand(ConfigurationCommandType.CM_STARTALL, "");

        }

        [Fact]
        public void When_we_build_a_control_bus_we_can_send_configuration_messages_to_it()
        {
            _exception = Catch.Exception(() => _controlBus.CommandProcessor.Send(_configurationCommand));

            //Should not raise exceptions for missing handlers
            Assert.Null(_exception);
            //Should call the dispatcher to start it
            A.CallTo(() => _dispatcher.Receive()).MustHaveHappened();
        }
        
        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
