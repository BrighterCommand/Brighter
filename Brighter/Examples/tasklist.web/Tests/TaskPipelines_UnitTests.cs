using System;
using FakeItEasy;
using Machine.Specifications;
using Newtonsoft.Json;
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
        static ITraceOutput traceOutput;
        static Exception exception;

        Establish context = () =>
        {
            tasksDAO = A.Fake<ITasksDAO>();
            A.CallTo(() => tasksDAO.Add(A<Task>.Ignored));
            traceOutput = A.Fake<ITraceOutput>();

            IAdaptAnInversionOfControlContainer container = new TinyIoCAdapter(new TinyIoCContainer());
            container.Register<ITasksDAO, ITasksDAO>(tasksDAO);
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();
            container.Register<ITraceOutput, ITraceOutput>(traceOutput);

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
        static ITraceOutput traceOutput;
        static Exception exception;

        Establish context = () =>
        {
            tasksDAO = A.Fake<ITasksDAO>();
            A.CallTo(() => tasksDAO.Add(A<Task>.Ignored));
            traceOutput = A.Fake<ITraceOutput>();

            IAdaptAnInversionOfControlContainer container = new TinyIoCAdapter(new TinyIoCContainer());
            container.Register<ITasksDAO, ITasksDAO>(tasksDAO);
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();
            container.Register<ITraceOutput, ITraceOutput>(traceOutput);

            commandProcessor = new CommandProcessor(container, new InMemoryRequestContextFactory());

            cmd = new AddTaskCommand(null, "Test that we store a task");
        };

        Because of = () => exception = Catch.Exception(() => commandProcessor.Send(cmd));

        It should_throw_a_validation_exception = () => exception.ShouldNotBeNull();
        It should_be_of_the_correct_type = () => exception.ShouldBeOfType<ArgumentException>();
        It should_show_a_suitable_message = () => exception.ShouldContainErrorMessage("The commmand was not valid");
    }

    public class When_adding_a_valid_new_task
    {
        static AddTaskCommand cmd;
        static IAmACommandProcessor commandProcessor;
        static ITasksDAO tasksDAO;
        static ITraceOutput traceOutput;

        Establish context = () =>
        {
            tasksDAO = A.Fake<ITasksDAO>();
            A.CallTo(() => tasksDAO.Add(A<Task>.Ignored));
            traceOutput = A.Fake<ITraceOutput>();

            IAdaptAnInversionOfControlContainer container = new TinyIoCAdapter(new TinyIoCContainer());
            container.Register<ITasksDAO, ITasksDAO>(tasksDAO);
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();
            container.Register<ITraceOutput, ITraceOutput>(traceOutput);

            commandProcessor = new CommandProcessor(container, new InMemoryRequestContextFactory());

            cmd = new AddTaskCommand("Test task", "Test that we store a task");
        };

        Because of = () => commandProcessor.Send(cmd);

        It should_add_the_task_to_the_task_List = () => A.CallTo(() => tasksDAO.Add(A<Task>.Ignored)).MustHaveHappened();
    }

    public class When_tracing_the_add_task_command_handler
    {
        static AddTaskCommand cmd;
        static IAmACommandProcessor commandProcessor;
        static ITasksDAO tasksDAO;
        static ITraceOutput traceOutput;

        Establish context = () =>
        {
            tasksDAO = A.Fake<ITasksDAO>();
            A.CallTo(() => tasksDAO.Add(A<Task>.Ignored));
            traceOutput = A.Fake<ITraceOutput>();

            IAdaptAnInversionOfControlContainer container = new TinyIoCAdapter(new TinyIoCContainer());
            container.Register<ITasksDAO, ITasksDAO>(tasksDAO);
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();
            container.Register<ITraceOutput, ITraceOutput>(traceOutput);

            commandProcessor = new CommandProcessor(container, new InMemoryRequestContextFactory());

            cmd = new AddTaskCommand("Test task", "Test that we store a task");
        };

        Because of = () => commandProcessor.Send(cmd);

        It should_log_the_call_before_the_command_handler = () => A.CallTo(() => traceOutput.WriteLine(string.Format("Calling handler for {0} with values of {1} at: {2}", typeof (AddTaskCommand), JsonConvert.SerializeObject(cmd), DateTime.UtcNow))).MustHaveHappened();
        It should_add_a_call_after_the_command_handler = () => A.CallTo(() => traceOutput.WriteLine(string.Format("Finished calling handler for {0} at: {1}", typeof (AddTaskCommand), DateTime.UtcNow))).MustHaveHappened();
    }
}