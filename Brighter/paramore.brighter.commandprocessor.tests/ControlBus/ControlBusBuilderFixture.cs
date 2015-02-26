// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
