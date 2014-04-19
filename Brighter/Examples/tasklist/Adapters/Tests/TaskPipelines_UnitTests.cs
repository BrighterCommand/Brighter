using System;
using Common.Logging;
using FakeItEasy;
using Machine.Specifications;
using Newtonsoft.Json;
using Tasklist.Adapters.DataAccess;
using Tasklist.Domain;
using Tasklist.Ports;
using Tasklist.Ports.Commands;
using Tasklist.Ports.Handlers;
using TinyIoC;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.ioccontainers.Adapters;

namespace Tasklist.Adapters.Tests
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
            var logger = A.Fake<ILog>();
            tasksDAO = A.Fake<ITasksDAO>();
            A.CallTo(() => tasksDAO.Add(A<Task>.Ignored));
            var log = A.Fake<ILog>();

            IAdaptAnInversionOfControlContainer container = new TinyIoCAdapter(new TinyIoCContainer());
            container.Register<ITasksDAO, ITasksDAO>(tasksDAO);
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();
            container.Register<ILog, ILog>(log);

            commandProcessor = new CommandProcessor(container, new InMemoryRequestContextFactory(), logger);

            cmd = new AddTaskCommand("Test task", null);
        };

        Because of = () => exception = Catch.Exception(() => commandProcessor.Send(cmd));

        It should_throw_a_validation_exception = () => exception.ShouldNotBeNull();
        It should_be_of_the_correct_type = () => exception.ShouldBeAssignableTo<ArgumentException>();
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
            var logger = A.Fake<ILog>();
            tasksDAO = A.Fake<ITasksDAO>();
            A.CallTo(() => tasksDAO.Add(A<Task>.Ignored));
            var log = A.Fake<ILog>();

            IAdaptAnInversionOfControlContainer container = new TinyIoCAdapter(new TinyIoCContainer());
            container.Register<ITasksDAO, ITasksDAO>(tasksDAO);
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();
            container.Register<ILog, ILog>(log);

            commandProcessor = new CommandProcessor(container, new InMemoryRequestContextFactory(), logger);

            cmd = new AddTaskCommand(null, "Test that we store a task");
        };

        Because of = () => exception = Catch.Exception(() => commandProcessor.Send(cmd));

        It should_throw_a_validation_exception = () => exception.ShouldNotBeNull();
        It should_be_of_the_correct_type = () => exception.ShouldBeAssignableTo<ArgumentException>();
        It should_show_a_suitable_message = () => exception.ShouldContainErrorMessage("The commmand was not valid");
    }

    public class When_adding_a_valid_new_task
    {
        static AddTaskCommand cmd;
        static IAmACommandProcessor commandProcessor;
        static ITasksDAO tasksDAO;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            tasksDAO = A.Fake<ITasksDAO>();
            A.CallTo(() => tasksDAO.Add(A<Task>.Ignored));
            var log = A.Fake<ILog>();

            IAdaptAnInversionOfControlContainer container = new TinyIoCAdapter(new TinyIoCContainer());
            container.Register<ITasksDAO, ITasksDAO>(tasksDAO);
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();
            container.Register<ILog, ILog>(log);

            commandProcessor = new CommandProcessor(container, new InMemoryRequestContextFactory(), logger);

            cmd = new AddTaskCommand("Test task", "Test that we store a task", DateTime.Now);
        };

        Because of = () => commandProcessor.Send(cmd);

        It should_add_the_task_to_the_task_List = () => A.CallTo(() => tasksDAO.Add(A<Task>.Ignored)).MustHaveHappened();
    }

    public class When_tracing_the_add_task_command_handler
    {
        static AddTaskCommand cmd;
        static IAmACommandProcessor commandProcessor;
        static ITasksDAO tasksDAO;
        private static ILog log;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            tasksDAO = A.Fake<ITasksDAO>();
            A.CallTo(() => tasksDAO.Add(A<Task>.Ignored));
            log = A.Fake<ILog>();

            IAdaptAnInversionOfControlContainer container = new TinyIoCAdapter(new TinyIoCContainer());
            container.Register<ITasksDAO, ITasksDAO>(tasksDAO);
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();
            container.Register<ILog, ILog>(log);

            commandProcessor = new CommandProcessor(container, new InMemoryRequestContextFactory(), logger);

            cmd = new AddTaskCommand("Test task", "Test that we store a task");
        };

        Because of = () => commandProcessor.Send(cmd);

        It should_log_the_call_before_the_command_handler = () => A.CallTo(() =>log.Debug(m => m("Logging handler pipeline call. Pipeline timing {0} target, for {1} with values of {2} at: {3}", HandlerTiming.Before.ToString(), typeof (AddTaskCommand), JsonConvert.SerializeObject(cmd), DateTime.UtcNow))).MustHaveHappened();
        It should_add_a_call_after_the_command_handler = () => A.CallTo(() =>log.Debug(m => m("Logging handler pipeline call. Pipeline timing {0} target, for {1} with values of {2} at: {3}", HandlerTiming.After.ToString(), typeof (AddTaskCommand), JsonConvert.SerializeObject(cmd), DateTime.UtcNow))).MustHaveHappened();
    }
}