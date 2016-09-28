using System;
using System.Linq;
using FakeItEasy;
using nUnitShouldAdapter;
using NUnit.Specifications;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using TinyIoC;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandProcessors
{
    [Subject(typeof(PipelineBuilder<>))]
    public class When_Building_A_Sync_Pipeline_That_Has_Async_Handlers : NUnit.Specifications.ContextSpecification
    {
        private static PipelineBuilder<MyCommand> s_pipeline_Builder;
        private static IHandleRequests<MyCommand> s_pipeline;
        private static Exception s_exception;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyMixedImplicitHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyMixedImplicitHandler>();
            container.Register<IHandleRequestsAsync<MyCommand>, MyLoggingHandlerAsync<MyCommand>>();
            container.Register<ILog>(logger);

            s_pipeline_Builder = new PipelineBuilder<MyCommand>(registry, handlerFactory, logger);
        };

        private Because _of = () => s_exception = Catch.Exception(() => s_pipeline = s_pipeline_Builder.Build(new RequestContext()).First());

        private It _should_throw_an_exception = () => s_exception.ShouldNotBeNull();
        private It _should_throw_a_configuration_exception_for_a_mixed_pipeline = () => s_exception.ShouldBeOfExactType(typeof (ConfigurationException));
        private It _should_include_the_erroneous_handler_name_in_the_exception = () => s_exception.Message.ShouldContain(typeof (MyLoggingHandlerAsync<>).Name);
    }
}
