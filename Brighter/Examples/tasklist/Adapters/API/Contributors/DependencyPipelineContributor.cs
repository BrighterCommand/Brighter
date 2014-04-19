using Common.Logging;
using OpenRasta.DI;
using OpenRasta.Pipeline;
using OpenRasta.Web;
using Tasklist.Adapters.DataAccess;
using Tasklist.Ports.Commands;
using Tasklist.Ports.Handlers;
using Tasklist.Ports.ViewModelRetrievers;
using TinyIoC;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.ioccontainers.Adapters;

namespace Tasklist.Adapters.API.Contributors
{
    public class DependencyPipelineContributor : IPipelineContributor
    {
        private readonly IDependencyResolver resolver;

        public DependencyPipelineContributor(IDependencyResolver resolver)
        {
            this.resolver = resolver;
        }

        public void Initialize(IPipeline pipelineRunner)
        {
            pipelineRunner.Notify(InitializeContainer)
                .Before<KnownStages.IOperationExecution>();
        }

        private PipelineContinuation InitializeContainer(ICommunicationContext arg)
        {
            var container = new TinyIoCAdapter(new TinyIoCContainer());
            //HACK! For now dependencies may need to be in both containers to allow resolution
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>().AsMultiInstance();
            container.Register<ITaskListRetriever, TaskListRetriever>().AsMultiInstance();
            container.Register<ITasksDAO, TasksDAO>().AsMultiInstance();
            var logger = LogManager.GetLogger("TaskList");
            container.Register<ILog, ILog>(logger).AsSingleton();

            resolver.AddDependencyInstance<IAdaptAnInversionOfControlContainer>(container, DependencyLifetime.Singleton);
            resolver.AddDependencyInstance<IAmARequestContextFactory>(new InMemoryRequestContextFactory(), DependencyLifetime.PerRequest);
            resolver.AddDependency<IAmACommandProcessor, CommandProcessor>(DependencyLifetime.Singleton);
            resolver.AddDependency<ITaskRetriever, TaskRetriever>(DependencyLifetime.Singleton);
            resolver.AddDependency<ITaskListRetriever, TaskListRetriever>(DependencyLifetime.Singleton);


            return PipelineContinuation.Continue;
        }
    }
}