using System;
using System.Linq;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Machine.Specifications;
using Paramore.Services.CommandHandlers;
using Paramore.Services.CommandProcessor;
using Paramore.Services.Commands;
using Paramore.Services.Common;

namespace Paramore.Tests.CommandProcessor
{
    [Subject(typeof(ChainofResponsibilityBuilder<>))]
    public class When_Finding_A_Handler_For_A_Command
    {
        private static ChainofResponsibilityBuilder<MyCommand> CHAIN_BUILDER;
        private static IHandleRequests<MyCommand> CHAIN_OF_RESPONSIBILITY;

        Establish context = () =>
        {
            var container = new WindsorContainer();

            container.Register(
                    Component.For<IHandleRequests<MyCommand>>().ImplementedBy<MyCommandHandler>()
                );

            CHAIN_BUILDER = new ChainofResponsibilityBuilder<MyCommand>(container); 
        };

        Because of = () => CHAIN_OF_RESPONSIBILITY = CHAIN_BUILDER.Build().First();

        It should_return_the_my_command_handler_as_the_implicit_handler = () => CHAIN_OF_RESPONSIBILITY.ShouldBeOfType(typeof(MyCommandHandler));
        It should_be_the_only_element_in_the_chain = () => GetChain().ToString().ShouldEqual("MyCommandHandler|");

        private static ChainPathExplorer GetChain()
        {
            var chainpathExplorer = new ChainPathExplorer();
            CHAIN_OF_RESPONSIBILITY.AddToChain(chainpathExplorer);
            return chainpathExplorer;
        }
    }

    #region Handlers and Commands
    internal class MyCommandHandler : RequestHandler<MyCommand>
    {
        public override MyCommand  Handle(MyCommand request)
        {
            return base.Handle(request);
        }
    }

    internal class MyCommand : ICommand, IRequest
    {
        public Guid Id { get; set; }
    }
    #endregion

    [Subject(typeof(ChainofResponsibilityBuilder<>))]
    public class When_A_Handler_Is_Part_of_A_Chain_of_Repsonsibility
    {
        private static ChainofResponsibilityBuilder<MyCommand> chainBuilder;
        private static IHandleRequests<MyCommand> chainOfResponsibility;

        Establish context = () =>
        {
            var container = new WindsorContainer();

            container.Register(Component.For<IHandleRequests<MyCommand>>().ImplementedBy<MyImplicitHandler>());

            chainBuilder = new ChainofResponsibilityBuilder<MyCommand>(container);
        };

        Because of = () => chainOfResponsibility = chainBuilder.Build().First();

        It should_include_my_command_handler_filter_in_the_chain = () => GetChain().ToString().Contains("MyImplicitHandler").ShouldBeTrue();
        It should_include_my_logging_handler_in_the_chain = () => GetChain().ToString().Contains("MyLoggingHander").ShouldBeTrue();

        private static ChainPathExplorer GetChain()
        {
            var chainpathExplorer = new ChainPathExplorer();
            chainOfResponsibility.AddToChain(chainpathExplorer);
            return chainpathExplorer;
        }
    }

    [Subject(typeof(ChainofResponsibilityBuilder<>))]
    public class When_Building_A_Chain_of_Repsonsibility_Preserve_The_Order
    {
        private static ChainofResponsibilityBuilder<MyCommand> chainBuilder;
        private static IHandleRequests<MyCommand> chainOfResponsibility;

        Establish context = () =>
        {
            var container = new WindsorContainer();

            container.Register(Component.For<IHandleRequests<MyCommand>>().ImplementedBy<MyDoubleDecoratedHandler>());

            chainBuilder = new ChainofResponsibilityBuilder<MyCommand>(container);
        };

        Because of = () => chainOfResponsibility = chainBuilder.Build().First();

        private It should_add_handlers_in_the_correct_sequence_into_the_chain = () => GetChain().ToString().ShouldEqual("MyLoggingHander`1|MyValidationHandler`1|MyDoubleDecoratedHandler|");

        private static ChainPathExplorer GetChain()
        {
            var chainpathExplorer = new ChainPathExplorer();
            chainOfResponsibility.AddToChain(chainpathExplorer);
            return chainpathExplorer;
        }
    }

    #region Handlers and Commands

    internal class MyLoggingHandlerAttribute : RequestHandlerAttribute
    {
        public MyLoggingHandlerAttribute(int step)
            : base(step)
        {
        }

        public override Type GetHandlerType()
        {
            return typeof(MyLoggingHander<>);
        }
    }

    internal class MyValidationHandlerAttribute : RequestHandlerAttribute
    {
        public MyValidationHandlerAttribute(int step)
            : base(step)
        {
        }

        public override Type GetHandlerType()
        {
            return typeof(MyValidationHandler<>);
        }
    }

    internal class MyValidationHandler<TRequest> : RequestHandler<TRequest> where TRequest : class, IRequest
    {
        public override TRequest Handle(TRequest request)
        {
            return request;
        }
    }

    internal class MyLoggingHander<TRequest> : RequestHandler<TRequest> where TRequest : class, IRequest
    {
        public override TRequest Handle(TRequest request)
        {
            return request;
        }
    }

    internal class MyImplicitHandler : RequestHandler<MyCommand>
    {
        [MyLoggingHandler(step:1)]
        public override MyCommand Handle(MyCommand request)
        {
            return base.Handle(request);
        }
    }

    internal class MyDoubleDecoratedHandler : RequestHandler<MyCommand>
    {
        [MyValidationHandler(step:2)]
        [MyLoggingHandler(step:1)]
        public override MyCommand Handle(MyCommand request)
        {
            return base.Handle(request);
        }
    }

    #endregion

    [Subject(typeof(ChainofResponsibilityBuilder<>))]
    public class When_Building_A_Chain_of_Repsonsibility_Allow_Pre_And_Post_Tasks
    {
        private static ChainofResponsibilityBuilder<MyCommand> chainBuilder;
        private static IHandleRequests<MyCommand> chainOfResponsibility;

        Establish context = () =>
        {
            var container = new WindsorContainer();

            container.Register(Component.For<IHandleRequests<MyCommand>>().ImplementedBy<MyPreAndPostDecoratedHandler>());

            chainBuilder = new ChainofResponsibilityBuilder<MyCommand>(container);
        };

        Because of = () => chainOfResponsibility = chainBuilder.Build().First();

        private It should_add_handlers_in_the_correct_sequence_into_the_chain = () => GetChain().ToString().ShouldEqual("MyValidationHandler`1|MyPreAndPostDecoratedHandler|MyLoggingHander`1|");

        private static ChainPathExplorer GetChain()
        {
            var chainpathExplorer = new ChainPathExplorer();
            chainOfResponsibility.AddToChain(chainpathExplorer);
            return chainpathExplorer;
        }
    }

    #region Handlers and Commands

    internal class MyPostLoggingHandlerAttribute : RequestHandlerAttribute
    {
        public MyPostLoggingHandlerAttribute(int step, HandlerTiming timing)
            : base(step, timing)
        {
        }

        public override Type GetHandlerType()
        {
            return typeof(MyLoggingHander<>);
        }
    }

    internal class MyPreValidationHandlerAttribute : RequestHandlerAttribute
    {
        public MyPreValidationHandlerAttribute(int step, HandlerTiming timing)
            : base(step, timing)
        {
        }

        public override Type GetHandlerType()
        {
            return typeof(MyValidationHandler<>);
        }
    }

    internal class MyPreAndPostDecoratedHandler : RequestHandler<MyCommand>
    {
        [MyPreValidationHandlerAttribute(step: 2, timing: HandlerTiming.Before)]
        [MyPostLoggingHandlerAttribute(step: 1, timing: HandlerTiming.After)]
        public override MyCommand Handle(MyCommand request)
        {
            return base.Handle(request);
        }
    }

    #endregion
    
}
