using System;
using Machine.Specifications;
using UserGroupManagement.ServiceLayer;
using UserGroupManagement.ServiceLayer.CommandProcessor;
using UserGroupManagement.ServiceLayer.Commands;

namespace UserGroupManagement.Tests.CommandProcessor
{
    [Subject("Our commmand processor implementation")]
    public class CommandProcessor
    {
        private static ChainofResponsibilityBuilder chainBuilder;
        private static ChainOfResponsibility chainOfResponsibility;

        private Establish context = () => { };

        private Because of = () => chainOfResponsibility = chainBuilder.Build(typeof (MyCommand));
    }

    internal class MyCommand : ICommand
    {
        public Guid Id { get; set; }
    }
}
