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
using Paramore.Brighter.ServiceActivator.TestHelpers;

namespace Paramore.Brighter.Core.Tests.ControlBus
{
    public class ControlBusTests
    {
        private readonly IDispatcher _dispatcher;
        private readonly Dispatcher _controlBus;
        private readonly ControlBusReceiverBuilder _busReceiverBuilder;
        private readonly ConfigurationCommand _configurationCommand;
        private Exception _exception;

        public ControlBusTests()
        {
            _dispatcher = A.Fake<IDispatcher>();
            var messageProducerFactory = A.Fake<IAmAMessageProducerFactory>();

            _busReceiverBuilder = (ControlBusReceiverBuilder) ControlBusReceiverBuilder
                .With()
                .Dispatcher(_dispatcher)
                .ProducerFactory(messageProducerFactory)
                .ChannelFactory(new InMemoryChannelFactory());

            _controlBus = _busReceiverBuilder.Build("tests");

            _configurationCommand = new ConfigurationCommand(ConfigurationCommandType.CM_STARTALL);

        }

        [Fact]
        public void When_we_build_a_control_bus_we_can_send_configuration_messages_to_it()
        {
            _exception = Catch.Exception(() => _controlBus.CommandProcessor.Send(_configurationCommand));

            //should_not_raise_exceptions_for_missing_handlers
            _exception.Should().BeNull();
            //should_call_the_dispatcher_to_start_it
            A.CallTo(() => _dispatcher.Receive()).MustHaveHappened();
        }
    }
}
