using System.Linq;
using Machine.Specifications;
using TinyIoC;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.ioccontainers.Adapters;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    [Subject(typeof(PipelineBuilder<>))]
    public class When_Finding_A_Handler_For_A_Command
    {
        private static PipelineBuilder<MyCommand> Pipeline_Builder;
        private static IHandleRequests<MyCommand> Pipeline;
        private static IAdaptAnInversionOfControlContainer Container;

        Establish context = () =>
        {
            Container = new TinyIoCAdapter(new TinyIoCContainer());
            Container.Register<IHandleRequests<MyCommand>, MyCommandHandler>().AsMultiInstance();            

            Pipeline_Builder = new PipelineBuilder<MyCommand>(Container); 
        };

        Because of = () => Pipeline = Pipeline_Builder.Build(new RequestContext(Container)).First();

        It should_return_the_my_command_handler_as_the_implicit_handler = () => Pipeline.ShouldBeAssignableTo(typeof(MyCommandHandler));
        It should_be_the_only_element_in_the_chain = () => TracePipeline().ToString().ShouldEqual("MyCommandHandler|");

        private static PipelineTracer TracePipeline()
        {
            var pipelineTracer = new PipelineTracer();
            Pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }

    [Subject(typeof(PipelineBuilder<>))]
    public class When_Finding_A_Hander_That_Has_Dependencies
    {
        private static PipelineBuilder<MyCommand> Pipeline_Builder;
        private static IHandleRequests<MyCommand> Pipeline;
        private static IAdaptAnInversionOfControlContainer Container;

        Establish context = () =>
        {
            Container = new TinyIoCAdapter(new TinyIoCContainer());

            Container.Register<IUnitOfWork, FakeSession>().AsMultiInstance();
            Container.Register<IRepository<MyAggregate>, FakeRepository<MyAggregate>>().AsMultiInstance();
            Container.Register<IHandleRequests<MyCommand>, MyDependentCommandHandler>().AsMultiInstance();

            Pipeline_Builder = new PipelineBuilder<MyCommand>(Container);
        };

        Because of = () => Pipeline = Pipeline_Builder.Build(new RequestContext(Container)).First();

        It should_return_the_command_handler_as_the_implicit_handler = () => Pipeline.ShouldBeAssignableTo(typeof(MyDependentCommandHandler));
        It should_be_the_only_element_in_the_chain = () => TracePipeline().ToString().ShouldEqual("MyDependentCommandHandler|");

        private static PipelineTracer TracePipeline()
        {
            var pipelineTracer = new PipelineTracer();
            Pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }      
    }

    [Subject(typeof(PipelineBuilder<>))]
    public class When_A_Handler_Is_Part_of_A_Pipeline
    {
        private static PipelineBuilder<MyCommand> Pipeline_Builder;
        private static IHandleRequests<MyCommand> Pipeline;
        private static IAdaptAnInversionOfControlContainer Container;

        Establish context = () =>
        {
            Container = new TinyIoCAdapter(new TinyIoCContainer());
            Container.Register<IHandleRequests<MyCommand>, MyImplicitHandler>().AsMultiInstance();

            Pipeline_Builder = new PipelineBuilder<MyCommand>(Container);
        };

        Because of = () => Pipeline = Pipeline_Builder.Build(new RequestContext(Container)).First();

        It should_include_my_command_handler_filter_in_the_chain = () => TracePipeline().ToString().Contains("MyImplicitHandler").ShouldBeTrue();
        It should_include_my_logging_handler_in_the_chain = () => TracePipeline().ToString().Contains("MyLoggingHandler").ShouldBeTrue();

        private static PipelineTracer TracePipeline()
        {
            var pipelineTracer = new PipelineTracer();
            Pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }

    [Subject(typeof(PipelineBuilder<>))]
    public class When_Building_A_Pipeline_Preserve_The_Order
    {
        private static PipelineBuilder<MyCommand> Pipeline_Builder;
        private static IHandleRequests<MyCommand> Pipeline;
        private static IAdaptAnInversionOfControlContainer Container;

        Establish context = () =>
        {
            Container = new TinyIoCAdapter(new TinyIoCContainer());
            Container.Register<IHandleRequests<MyCommand>, MyDoubleDecoratedHandler>().AsMultiInstance();
            Pipeline_Builder = new PipelineBuilder<MyCommand>(Container);
        };

        Because of = () => Pipeline = Pipeline_Builder.Build(new RequestContext(Container)).First();

        It should_add_handlers_in_the_correct_sequence_into_the_chain = () => PipelineTracer().ToString().ShouldEqual("MyLoggingHandler`1|MyValidationHandler`1|MyDoubleDecoratedHandler|");

        private static PipelineTracer PipelineTracer()
        {
            var pipelineTracer = new PipelineTracer();
            Pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }

    [Subject(typeof(PipelineBuilder<>))]
    public class When_Building_A_Pipeline_Allow_Pre_And_Post_Tasks
    {
        private static PipelineBuilder<MyCommand> Pipeline_Builder;
        private static IHandleRequests<MyCommand> Pipeline;
        private static IAdaptAnInversionOfControlContainer Container;

        Establish context = () =>
        {
            Container = new TinyIoCAdapter(new TinyIoCContainer());
            Container.Register<IHandleRequests<MyCommand>, MyPreAndPostDecoratedHandler>().AsMultiInstance();
            Pipeline_Builder = new PipelineBuilder<MyCommand>(Container);
        };

        Because of = () => Pipeline = Pipeline_Builder.Build(new RequestContext(Container)).First();

        It should_add_handlers_in_the_correct_sequence_into_the_chain = () => TraceFilters().ToString().ShouldEqual("MyValidationHandler`1|MyPreAndPostDecoratedHandler|MyLoggingHandler`1|");

        private static PipelineTracer TraceFilters()
        {
            var pipelineTracer = new PipelineTracer();
            Pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }

    [Subject(typeof(PipelineBuilder<>))]
    public class When_we_have_exercised_the_pipeline_cleanup_its_handlers
    {
        private static PipelineBuilder<MyCommand> Pipeline_Builder;
        private static IAdaptAnInversionOfControlContainer Container;
        private static int trackedItemCount;
        private static int decoratorCount;

        Establish context = () =>
        {
            Container = new TinyIoCAdapter(new TinyIoCContainer());
            Container.Register<IHandleRequests<MyCommand>, MyPreAndPostDecoratedHandler>().AsMultiInstance();
            Pipeline_Builder = new PipelineBuilder<MyCommand>(Container);
            Pipeline_Builder.Build(new RequestContext(Container)).First();
            trackedItemCount = Container.TrackedItemCount;
            decoratorCount = Pipeline_Builder.Decorators.Count();
        };

        Because of = () => Pipeline_Builder.Dispose(); 

        It should_have_a_tracked_item_count_equal_to_number_of_handlers_before = () => trackedItemCount.ShouldEqual(1);
        It should_have_no_tracked_items_once_disposed = () => Container.TrackedItemCount.ShouldEqual(0);
        It should_have_two_decorators_once_the_pipeline_is_built = () => decoratorCount.ShouldEqual(2);
        It should_have_no_decorators_once_the_pipeline_builder_is_torn_down = () => Pipeline_Builder.Decorators.Count().ShouldEqual(0);
        It should_have_called_dispose_on_instances_from_ioc = () => MyPreAndPostDecoratedHandler.DisposeWasCalled.ShouldBeTrue();
        It should_have_called_dispose_on_instances_from_pipeline_builder = () => MyLoggingHandler<MyCommand>.DisposeWasCalled.ShouldBeTrue();
    }

    [Subject(typeof(PipelineBuilder<>))]
    public class When_we_cleanup_do_not_dispose_of_singletons
    {
        private static PipelineBuilder<MyCommand> Pipeline_Builder;
        private static IAdaptAnInversionOfControlContainer Container;
        private static int trackedItemCount;
        
        Establish context = () =>
        {
            Container = new TinyIoCAdapter(new TinyIoCContainer());
            Container.Register<IHandleRequests<MyCommand>, MyPreAndPostDecoratedHandler>().AsSingleton();
            Pipeline_Builder = new PipelineBuilder<MyCommand>(Container);
            Pipeline_Builder.Build(new RequestContext(Container)).First();
            trackedItemCount = Container.TrackedItemCount;
        };

        Because of = () => Pipeline_Builder.Dispose();

        It should_not_add_the_singleton_to_the_tracked_list = () => trackedItemCount.ShouldEqual(0);

        It should_not_call_dispose_on_the_singleton = () => MyPreAndPostDecoratedHandler.DisposeWasCalled.ShouldBeFalse();
    }
    
}
