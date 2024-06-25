#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using FakeItEasy;
using FluentAssertions;
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

            _busReceiverBuilder = ControlBusReceiverBuilder
                .With()
                .Dispatcher(dispatcher)
                .ProducerRegistryFactory(new InMemoryProducerRegistryFactory(bus, new []
                    {
                        new Publication{Topic = new RoutingKey("MyTopic"), RequestType = typeof(ConfigurationCommand)}
                    }))
                .ChannelFactory(new InMemoryChannelFactory(bus, TimeProvider.System)) as ControlBusReceiverBuilder;
        }

        [Fact]
        public void When_configuring_a_control_bus()
        {
            _controlBus = _busReceiverBuilder.Build(_hostName);

            _controlBus.Connections.Should().Contain(cn => cn.Name == $"{_hostName}.{ControlBusReceiverBuilder.CONFIGURATION}");
            _controlBus.Connections.Should().Contain(cn => cn.Name == $"{_hostName}.{ControlBusReceiverBuilder.HEARTBEAT}");
            _controlBus.CommandProcessor.Should().NotBeNull();
        }
        
        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
