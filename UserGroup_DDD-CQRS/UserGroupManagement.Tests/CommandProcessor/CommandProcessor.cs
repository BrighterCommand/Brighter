using System;
using Castle.Windsor;
using Castle.MicroKernel.Registration;
using Machine.Specifications;
using UserGroupManagement.ServiceLayer.CommandHandlers;
using UserGroupManagement.ServiceLayer.CommandProcessor;
using UserGroupManagement.ServiceLayer.Commands;

namespace UserGroupManagement.Tests.CommandProcessor
{
    [Subject("Our commmand processor implementation")]
    public class CommandProcessorTests
    {
        private static ChainofResponsibilityBuilder<MyCommand> chainBuilder;
        private static IHandleRequests<MyCommand> chainOfResponsibility;

        Establish context = () =>
                                {
                                    var container = new WindsorContainer();

                                    container.Register(
                                         Component.For<IHandleRequests<MyCommand>>().ImplementedBy<MyCommandHandler>()
                                        );

                                    chainBuilder = new ChainofResponsibilityBuilder<MyCommand>(container); 
                                };

        Because of = () => chainOfResponsibility = chainBuilder.Build();

        It should_return_the_my_command_handler_as_the_implicit_handler = () => chainOfResponsibility.ShouldBeOfType(typeof (MyCommandHandler));
        It should_be_the_only_element_in_the_chain = () => GetChain().ToString().ShouldEqual("MyCommandHandler");

        private static ChainPathExplorer GetChain()
        {
            var chainpathExplorer = new ChainPathExplorer();
            chainOfResponsibility.AddToChain(chainpathExplorer);
            return chainpathExplorer;
        }
    }

    internal class MyCommandHandler : RequestHandler<MyCommand>
    {
        public override MyCommand  Handle(MyCommand request)
        {
            return base.Handle(request);
        }
    }

    internal class MyCommand : ICommand
    {
        public Guid Id { get; set; }
    }
}
