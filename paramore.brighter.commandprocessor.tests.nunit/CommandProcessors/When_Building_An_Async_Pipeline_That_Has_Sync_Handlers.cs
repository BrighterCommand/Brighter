using System;
using System.Linq;
using nUnitShouldAdapter;
using NUnit.Specifications;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using TinyIoC;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandProcessors
{
    [Subject(typeof(PipelineBuilder<>))]
    public class When_Building_An_Async_Pipeline_That_Has_Sync_Handlers : ContextSpecification
    {
        private static PipelineBuilder<MyCommand> s_pipelineBuilder;
        private static IHandleRequestsAsync<MyCommand> s_pipeline;
        private static Exception s_exception;

        private Establish _context = () =>
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyMixedImplicitHandlerAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyMixedImplicitHandlerAsync>();
            container.Register<IHandleRequests<MyCommand>, MyLoggingHandler<MyCommand>>();

            s_pipelineBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory);
        };

        private Because _of = () => s_exception = Catch.Exception(() => s_pipeline = s_pipelineBuilder.BuildAsync(new RequestContext(), false).First());

        private It _should_throw_an_exception = () => s_exception.ShouldNotBeNull();
        private It _should_throw_a_configuration_exception_for_a_mixed_pipeline = () => s_exception.ShouldBeOfExactType(typeof (ConfigurationException));
        private It _should_include_the_erroneous_handler_name_in_the_exception = () => s_exception.Message.ShouldContain(typeof (MyLoggingHandler<>).Name);
    }
}
