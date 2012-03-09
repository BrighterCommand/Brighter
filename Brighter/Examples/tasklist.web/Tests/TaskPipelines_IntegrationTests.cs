using FakeItEasy;
using Machine.Specifications;
using Simple.Data;
using TinyIoC;
using paramore.commandprocessor;
using paramore.commandprocessor.ioccontainers.IoCContainers;
using tasklist.web.Commands;
using tasklist.web.DataAccess;
using tasklist.web.Handlers;
using tasklist.web.Utilities;

namespace tasklist.web.Tests
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

            cmd = new AddTaskCommand("New Task", "Test that we store a task");
        };

        Because of = () => commandProcessor.Send(cmd);

        It should_store_something_in_the_db = () => new TasksDAO().FindByName(cmd.TaskName).ShouldNotBeNull();

    }
}