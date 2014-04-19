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

using System.Linq;
using Common.Logging;
using Common.Logging.Simple;
using FakeItEasy;
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
            var logger = A.Fake<ILog>();
            Container = new TinyIoCAdapter(new TinyIoCContainer());
            Container.Register<IHandleRequests<MyCommand>, MyCommandHandler>().AsMultiInstance();
            Container.Register<ILog, ILog>(logger);

            Pipeline_Builder = new PipelineBuilder<MyCommand>(Container, logger); 
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
            var logger = A.Fake<ILog>();
            Container = new TinyIoCAdapter(new TinyIoCContainer());

            Container.Register<IUnitOfWork, FakeSession>().AsMultiInstance();
            Container.Register<IRepository<MyAggregate>, FakeRepository<MyAggregate>>().AsMultiInstance();
            Container.Register<IHandleRequests<MyCommand>, MyDependentCommandHandler>().AsMultiInstance();
            Container.Register<ILog, ILog>(logger);

            Pipeline_Builder = new PipelineBuilder<MyCommand>(Container, logger);
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
            var logger = A.Fake<ILog>();
            Container = new TinyIoCAdapter(new TinyIoCContainer());
            Container.Register<IHandleRequests<MyCommand>, MyImplicitHandler>().AsMultiInstance();
            Container.Register<ILog, ILog>(logger);

            Pipeline_Builder = new PipelineBuilder<MyCommand>(Container, logger);
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
            var logger = A.Fake<ILog>();
            Container = new TinyIoCAdapter(new TinyIoCContainer());
            Container.Register<IHandleRequests<MyCommand>, MyDoubleDecoratedHandler>().AsMultiInstance();
            Container.Register<ILog, ILog>(logger);
            Pipeline_Builder = new PipelineBuilder<MyCommand>(Container, logger);
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
            var logger = A.Fake<ILog>();
            Container = new TinyIoCAdapter(new TinyIoCContainer());
            Container.Register<IHandleRequests<MyCommand>, MyPreAndPostDecoratedHandler>().AsMultiInstance();
            Container.Register<ILog, ILog>(logger);
            Pipeline_Builder = new PipelineBuilder<MyCommand>(Container, logger);
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
            var logger = A.Fake<ILog>();
            Container = new TinyIoCAdapter(new TinyIoCContainer());
            Container.Register<IHandleRequests<MyCommand>, MyPreAndPostDecoratedHandler>().AsMultiInstance();
            Container.Register<ILog, NoOpLogger>().AsSingleton();
            Pipeline_Builder = new PipelineBuilder<MyCommand>(Container, logger);
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
            var logger = A.Fake<ILog>();
            Container = new TinyIoCAdapter(new TinyIoCContainer());
            Container.Register<IHandleRequests<MyCommand>, MyPreAndPostDecoratedHandler>().AsSingleton();
            Container.Register<ILog, NoOpLogger>().AsSingleton();
            Pipeline_Builder = new PipelineBuilder<MyCommand>(Container, logger);
            Pipeline_Builder.Build(new RequestContext(Container)).First();
            trackedItemCount = Container.TrackedItemCount;
        };

        Because of = () => Pipeline_Builder.Dispose();

        It should_not_add_the_singleton_to_the_tracked_list = () => trackedItemCount.ShouldEqual(0);

        It should_not_call_dispose_on_the_singleton = () => MyPreAndPostDecoratedHandler.DisposeWasCalled.ShouldBeFalse();
    }
    
}
