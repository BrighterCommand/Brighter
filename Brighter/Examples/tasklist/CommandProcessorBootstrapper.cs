using Tasklist.Adapters.DataAccess;
using Tasklist.Ports;
using Tasklist.Ports.Commands;
using Tasklist.Ports.Handlers;
using Tasklist.Ports.ViewModelRetrievers;
using Tasklist.Utilities;
using paramore.commandprocessor;
using paramore.commandprocessor.ioccontainers.IoCContainers;

namespace Tasklist
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