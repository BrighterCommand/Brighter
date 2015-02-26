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
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using TinyIoC;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    [Subject(typeof(PipelineBuilder<>))]
    public class When_Finding_A_Handler_For_A_Command
    {
        private static PipelineBuilder<MyCommand> s_pipeline_Builder;
        private static IHandleRequests<MyCommand> s_pipeline;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyCommandHandler>();
            var handlerFactory = new TestHandlerFactory<MyCommand, MyCommandHandler>(() => new MyCommandHandler(logger));

            s_pipeline_Builder = new PipelineBuilder<MyCommand>(registry, handlerFactory, logger);
        };

        private Because _of = () => s_pipeline = s_pipeline_Builder.Build(new RequestContext()).First();

        private It _should_return_the_my_command_handler_as_the_implicit_handler = () => s_pipeline.ShouldBeAssignableTo(typeof(MyCommandHandler));
        private It _should_be_the_only_element_in_the_chain = () => TracePipeline().ToString().ShouldEqual("MyCommandHandler|");

        private static PipelineTracer TracePipeline()
        {
            var pipelineTracer = new PipelineTracer();
            s_pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }

    [Subject(typeof(PipelineBuilder<>))]
    public class When_Finding_A_Hander_That_Has_Dependencies
    {
        private static PipelineBuilder<MyCommand> s_pipeline_Builder;
        private static IHandleRequests<MyCommand> s_pipeline;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyDependentCommandHandler>();
            var handlerFactory = new TestHandlerFactory<MyCommand, MyDependentCommandHandler>(() => new MyDependentCommandHandler(new FakeRepository<MyAggregate>(new FakeSession()), logger));

            s_pipeline_Builder = new PipelineBuilder<MyCommand>(registry, handlerFactory, logger);
        };

        private Because _of = () => s_pipeline = s_pipeline_Builder.Build(new RequestContext()).First();

        private It _should_return_the_command_handler_as_the_implicit_handler = () => s_pipeline.ShouldBeAssignableTo(typeof(MyDependentCommandHandler));
        private It _should_be_the_only_element_in_the_chain = () => TracePipeline().ToString().ShouldEqual("MyDependentCommandHandler|");

        private static PipelineTracer TracePipeline()
        {
            var pipelineTracer = new PipelineTracer();
            s_pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }

    [Subject(typeof(PipelineBuilder<>))]
    public class When_A_Handler_Is_Part_of_A_Pipeline
    {
        private static PipelineBuilder<MyCommand> s_pipeline_Builder;
        private static IHandleRequests<MyCommand> s_pipeline;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyImplicitHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyImplicitHandler>();
            container.Register<IHandleRequests<MyCommand>, MyLoggingHandler<MyCommand>>();
            container.Register<ILog>(logger);

            s_pipeline_Builder = new PipelineBuilder<MyCommand>(registry, handlerFactory, logger);
        };

        private Because _of = () => s_pipeline = s_pipeline_Builder.Build(new RequestContext()).First();

        private It _should_include_my_command_handler_filter_in_the_chain = () => TracePipeline().ToString().Contains("MyImplicitHandler").ShouldBeTrue();
        private It _should_include_my_logging_handler_in_the_chain = () => TracePipeline().ToString().Contains("MyLoggingHandler").ShouldBeTrue();

        private static PipelineTracer TracePipeline()
        {
            var pipelineTracer = new PipelineTracer();
            s_pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }

    [Subject(typeof(PipelineBuilder<>))]
    public class When_Building_A_Pipeline_Preserve_The_Order
    {
        private static PipelineBuilder<MyCommand> s_pipeline_Builder;
        private static IHandleRequests<MyCommand> s_pipeline;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyDoubleDecoratedHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyDoubleDecoratedHandler>();
            container.Register<IHandleRequests<MyCommand>, MyValidationHandler<MyCommand>>();
            container.Register<IHandleRequests<MyCommand>, MyLoggingHandler<MyCommand>>();
            container.Register<ILog>(logger);

            s_pipeline_Builder = new PipelineBuilder<MyCommand>(registry, handlerFactory, logger);
        };

        private Because _of = () => s_pipeline = s_pipeline_Builder.Build(new RequestContext()).First();

        private It _should_add_handlers_in_the_correct_sequence_into_the_chain = () => PipelineTracer().ToString().ShouldEqual("MyLoggingHandler`1|MyValidationHandler`1|MyDoubleDecoratedHandler|");

        private static PipelineTracer PipelineTracer()
        {
            var pipelineTracer = new PipelineTracer();
            s_pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }

    [Subject(typeof(PipelineBuilder<>))]
    public class When_Building_A_Pipeline_Allow_Pre_And_Post_Tasks
    {
        private static PipelineBuilder<MyCommand> s_pipeline_Builder;
        private static IHandleRequests<MyCommand> s_pipeline;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyPreAndPostDecoratedHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyPreAndPostDecoratedHandler>();
            container.Register<IHandleRequests<MyCommand>, MyValidationHandler<MyCommand>>();
            container.Register<IHandleRequests<MyCommand>, MyLoggingHandler<MyCommand>>();
            container.Register<ILog>(logger);

            s_pipeline_Builder = new PipelineBuilder<MyCommand>(registry, handlerFactory, logger);
        };

        private Because _of = () => s_pipeline = s_pipeline_Builder.Build(new RequestContext()).First();

        private It _should_add_handlers_in_the_correct_sequence_into_the_chain = () => TraceFilters().ToString().ShouldEqual("MyValidationHandler`1|MyPreAndPostDecoratedHandler|MyLoggingHandler`1|");

        private static PipelineTracer TraceFilters()
        {
            var pipelineTracer = new PipelineTracer();
            s_pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }

    [Subject(typeof(PipelineBuilder<>))]
    public class When_Building_A_Pipeline_Allow_ForiegnAttribues
    {
        private static PipelineBuilder<MyCommand> s_pipeline_Builder;
        private static IHandleRequests<MyCommand> s_pipeline;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyObsoleteCommandHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyObsoleteCommandHandler>();
            container.Register<IHandleRequests<MyCommand>, MyValidationHandler<MyCommand>>();
            container.Register<IHandleRequests<MyCommand>, MyLoggingHandler<MyCommand>>();
            container.Register<ILog>(logger);

            s_pipeline_Builder = new PipelineBuilder<MyCommand>(registry, handlerFactory, logger);
        };

        private Because _of = () => s_pipeline = s_pipeline_Builder.Build(new RequestContext()).First();

        private It _should_add_handlers_in_the_correct_sequence_into_the_chain = () => TraceFilters().ToString().ShouldEqual("MyValidationHandler`1|MyObsoleteCommandHandler|MyLoggingHandler`1|");

        private static PipelineTracer TraceFilters()
        {
            var pipelineTracer = new PipelineTracer();
            s_pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }

    [Subject(typeof(PipelineBuilder<>))]
    public class When_we_have_exercised_the_pipeline_cleanup_its_handlers
    {
        private static PipelineBuilder<MyCommand> s_pipeline_Builder;
        private static string s_released;

        private Establish _context = () =>
        {
            s_released = string.Empty;
            var logger = A.Fake<ILog>();


            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyPreAndPostDecoratedHandler>();
            registry.Register<MyCommand, MyLoggingHandler<MyCommand>>();

            var handlerFactory = new CheapHandlerFactory();

            s_pipeline_Builder = new PipelineBuilder<MyCommand>(registry, handlerFactory, logger);
            s_pipeline_Builder.Build(new RequestContext()).Any();
        };

        internal class CheapHandlerFactory : IAmAHandlerFactory
        {
            public IHandleRequests Create(Type handlerType)
            {
                var logger = A.Fake<ILog>();
                if (handlerType == typeof(MyPreAndPostDecoratedHandler))
                {
                    return new MyPreAndPostDecoratedHandler(logger);
                }
                if (handlerType == typeof(MyLoggingHandler<MyCommand>))
                {
                    return new MyLoggingHandler<MyCommand>(logger);
                }
                if (handlerType == typeof(MyValidationHandler<MyCommand>))
                {
                    return new MyValidationHandler<MyCommand>(logger);
                }
                return null;
            }

            public void Release(IHandleRequests handler)
            {
                var disposable = handler as IDisposable;
                if (disposable != null)
                    disposable.Dispose();

                s_released += "|" + handler.Name;
            }
        }


        private Because _of = () => s_pipeline_Builder.Dispose();

        private It _should_have_called_dispose_on_instances_from_ioc = () => MyPreAndPostDecoratedHandler.DisposeWasCalled.ShouldBeTrue();
        private It _should_have_called_dispose_on_instances_from_pipeline_builder = () => MyLoggingHandler<MyCommand>.DisposeWasCalled.ShouldBeTrue();
        private It _should_have_called_release_on_all_handlers = () => s_released.ShouldEqual("|MyValidationHandler`1|MyPreAndPostDecoratedHandler|MyLoggingHandler`1|MyLoggingHandler`1");
    }
}
