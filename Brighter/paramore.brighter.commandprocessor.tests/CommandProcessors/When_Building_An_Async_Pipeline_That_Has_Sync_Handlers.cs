using System;
using System.Linq;
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using TinyIoC;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    [Subject(typeof(PipelineBuilder<>))]
    public class When_Building_An_Async_Pipeline_That_Has_Sync_Handlers
    {
        private static PipelineBuilder<MyCommand> s_pipeline_Builder;
        private static IHandleRequestsAsync<MyCommand> s_pipeline;
        private static Exception s_exception;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyMixedImplicitHandlerRequestHandlerAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyMixedImplicitHandlerRequestHandlerAsync>();
            container.Register<IHandleRequests<MyCommand>, MyLoggingHandler<MyCommand>>();
            container.Register<ILog>(logger);

            s_pipeline_Builder = new PipelineBuilder<MyCommand>(registry, handlerFactory, logger);
        };

        private Because _of = () => s_exception = Catch.Exception(() => s_pipeline = s_pipeline_Builder.BuildAsync(new RequestContext(), false).First());

        private It _should_throw_an_exception = () => s_exception.ShouldNotBeNull();
        private It _should_throw_a_configuration_exception_for_a_mixed_pipeline = () => s_exception.ShouldBeOfExactType(typeof (ConfigurationException));
        private It _should_include_the_erroneous_handler_name_in_the_exception = () => s_exception.Message.ShouldContain(typeof (MyLoggingHandler<>).Name);
    }
}
