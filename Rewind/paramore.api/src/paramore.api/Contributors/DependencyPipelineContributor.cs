using Paramore.Adapters.Infrastructure.Repositories;
using OpenRasta.DI;
using OpenRasta.Pipeline;
using OpenRasta.Web;

namespace Paramore.Adapters.Presentation.API.Contributors
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
            pipelineRunner.Notify(CreateUnitOfWork)
                .Before<KnownStages.IOperationExecution>();
        }

        private PipelineContinuation CreateUnitOfWork(ICommunicationContext arg)
        {
            resolver.AddDependencyInstance<IAmAUnitOfWorkFactory>(new UnitOfWorkFactory(), DependencyLifetime.PerRequest);
            return PipelineContinuation.Continue;
        }
    }
}