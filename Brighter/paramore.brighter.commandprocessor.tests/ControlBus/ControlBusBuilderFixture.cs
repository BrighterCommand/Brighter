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
        static Dispatcher controlBus;
        static ControlBusBuilder busBuilder;

        Establish context = () =>
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

            busBuilder = ControlBusBuilder
                .With()
                .Logger(logger)
                .CommandProcessor(commandProcessor)
                .ChannelFactory(new InMemoryChannelFactory()) as ControlBusBuilder;

        };
        
        Because of = () => controlBus = busBuilder.Build("tests");

        It should_have_a_configuration_channel = () => controlBus.Connections.Any(cn => cn.Name == ControlBusBuilder.CONFIGURATION).ShouldBeTrue();
        It should_have_a_heartbeat_channel = () => controlBus.Connections.Any(cn => cn.Name == ControlBusBuilder.HEARTBEAT).ShouldBeTrue();
    }
}
