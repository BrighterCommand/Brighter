using Nancy;
using paramore.commandprocessor;
using paramore.commandprocessor.ioccontainers.IoCContainers;
using tasklist.web.Commands;
using tasklist.web.DataAccess;
using tasklist.web.Handlers;
using tasklist.web.Utilities;
using tasklist.web.ViewModelRetrievers;

namespace tasklist.web
{
    public class CommandProcessorBootstrapper : DefaultNancyBootstrapper
    {
        protected override void ConfigureApplicationContainer(TinyIoC.TinyIoCContainer container)
        {
            base.ConfigureApplicationContainer(container);
            //try to register the command handlers
            container.Register<IAmACommandProcessor, CommandProcessor>();
            container.Register<IAdaptAnInversionOfControlContainer, TinyIoCAdapter>();
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>().AsMultiInstance();
            container.Register<ITaskListRetriever, TaskListRetriever>().AsMultiInstance();
            container.Register<ITasksDAO, TasksDAO>().AsMultiInstance();
            container.Register<ITraceOutput, ConsoleTrace>().AsSingleton();
        }
    }
}