using System;
using FakeItEasy;
using Machine.Specifications;
using Tasklist.Adapters.DataAccess;
using Tasklist.Domain;
using Tasklist.Ports;
using Tasklist.Ports.Commands;
using Tasklist.Ports.Handlers;
using Tasklist.Utilities;
using TinyIoC;
using paramore.commandprocessor;
using paramore.commandprocessor.ioccontainers.IoCContainers;

namespace Tasklist.Adapters.Tests
{
    public class When_I_add_a_new_task
    {
        static AddTaskCommand cmd;
        static IAmACommandProcessor commandProcessor;
        static readonly RequestContext requestContext = new RequestContext();

        Establish context = () =>
        {
            IAdaptAnInversionOfControlContainer container = new TinyIoCAdapter(new TinyIoCContainer());
            container.Register<ITasksDAO, TasksDAO>();
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>();
            container.Register<ITraceOutput, ConsoleTrace>();

            var requestContextFactory = A.Fake<IAmARequestContextFactory>();
            A.CallTo(() => requestContextFactory.Create()).Returns(requestContext);

            commandProcessor = new CommandProcessor(container, requestContextFactory);

            cmd = new AddTaskCommand("New Task", "Test that we store a task", DateTime.Now.AddDays(3));

            new TasksDAO().Clear();
        };

        Because of = () => commandProcessor.Send(cmd);

        It should_store_something_in_the_db = () => FindTaskinDb().ShouldNotBeNull();
        It should_store_with_the_correct_name = () => FindTaskinDb().TaskName.ShouldEqual(cmd.TaskName);
        It should_store_with_the_correct_description = () => FindTaskinDb().TaskDescription.ShouldEqual(cmd.TaskDecription);
        It should_store_with_the_correct_duedate = () => FindTaskinDb().DueDate.ShouldEqual(cmd.TaskDueDate);

        static Task FindTaskinDb()
        {
            return new TasksDAO().FindByName(cmd.TaskName);
        }
    }
}