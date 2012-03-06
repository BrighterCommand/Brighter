using System;
using FakeItEasy;
using Machine.Specifications;
using TinyIoC;
using paramore.commandprocessor;
using paramore.commandprocessor.ioccontainers.IoCContainers;
using tasklist.web.Commands;
using tasklist.web.DataAccess;
using tasklist.web.Handlers;
using tasklist.web.Models;

namespace tasklist.web.Tests
{
    [Subject(typeof(AddTaskCommandHandler))]
    public class When_a_new_task_is_missing_the_description
    {
        static AddTaskCommand cmd;
        static IAmACommandProcessor commandProcessor;
        static ITasksDAO tasksDAO;
        static Exception exception;

        Establish context = () =>
        {
            tasksDAO = A.Fake<ITasksDAO>();
            A.CallTo(() => tasksDAO.Add(A<Task>.Ignored));

            IAdaptAnInversionOfControlContainer container = new TinyIoCAdapter(new TinyIoCContainer());
            container.Register<ITasksDAO, ITasksDAO>(tasksDAO);
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();

            commandProcessor = new CommandProcessor(container, new InMemoryRequestContextFactory());

            cmd = new AddTaskCommand("Test task", null);
        };

        Because of = () => exception = Catch.Exception(() => commandProcessor.Send(cmd));

        It should_throw_a_validation_exception = () => exception.ShouldNotBeNull();
        It should_be_of_the_correct_type = () => exception.ShouldBeOfType<ArgumentException>();
        It should_show_a_suitable_message = () => exception.ShouldContainErrorMessage("The commmand was not valid");
    }

    [Subject(typeof(AddTaskCommandHandler))]
    public class When_a_new_task_is_missing_the_name
    {
        static AddTaskCommand cmd;
        static IAmACommandProcessor commandProcessor;
        static ITasksDAO tasksDAO;
        static Exception exception;

        Establish context = () =>
        {
            tasksDAO = A.Fake<ITasksDAO>();
            A.CallTo(() => tasksDAO.Add(A<Task>.Ignored));

            IAdaptAnInversionOfControlContainer container = new TinyIoCAdapter(new TinyIoCContainer());
            container.Register<ITasksDAO, ITasksDAO>(tasksDAO);
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();

            commandProcessor = new CommandProcessor(container, new InMemoryRequestContextFactory());

            cmd = new AddTaskCommand(null, "Test that we store a task");
        };

        Because of = () => exception = Catch.Exception(() => commandProcessor.Send(cmd));

        It should_throw_a_validation_exception = () => exception.ShouldNotBeNull();
        It should_be_of_the_correct_type = () => exception.ShouldBeOfType<ArgumentException>();
        It should_show_a_suitable_message = () => exception.ShouldContainErrorMessage("The commmand was not valid");
    }
}