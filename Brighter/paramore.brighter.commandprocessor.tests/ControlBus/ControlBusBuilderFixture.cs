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
using System.Linq;
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.controlbus;
using paramore.brighter.serviceactivator.TestHelpers;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using Polly;
using TinyIoC;

namespace paramore.commandprocessor.tests.ControlBus
{
    public class When_configuring_a_control_bus
    {
        private static Dispatcher s_controlBus;
        private static ControlBusBuilder s_busBuilder;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetry(new[]
                {
                    TimeSpan.FromMilliseconds(50),
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(150)
                });

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(500));

            var commandProcessor = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(new SubscriberRegistry(), new TinyIocHandlerFactory(new TinyIoCContainer())))
                .Policies(new PolicyRegistry()
                {
                    {CommandProcessor.RETRYPOLICY, retryPolicy},
                    {CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy}
                })
                .Logger(logger)
                .NoTaskQueues()
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build();

            s_busBuilder = ControlBusBuilder
                .With()
                .Logger(logger)
                .CommandProcessor(commandProcessor)
                .ChannelFactory(new InMemoryChannelFactory()) as ControlBusBuilder;
        };

        private Because _of = () => s_controlBus = s_busBuilder.Build("tests");

        private It _should_have_a_configuration_channel = () => s_controlBus.Connections.Any(cn => cn.Name == ControlBusBuilder.CONFIGURATION).ShouldBeTrue();
        private It _should_have_a_heartbeat_channel = () => s_controlBus.Connections.Any(cn => cn.Name == ControlBusBuilder.HEARTBEAT).ShouldBeTrue();
    }
}
