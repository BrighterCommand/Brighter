// ReSharper disable RedundantUsingDirective
using OpenRasta.DI;
using OpenRasta.Pipeline;
using OpenRasta.Web;
// ReSharper restore RedundantUsingDirective
using Paramore.Adapters.Infrastructure.Repositories;

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
            pipelineRunner.Notify(AddDependencies)
                .Before<KnownStages.IOperationExecution>();
        }

        private PipelineContinuation AddDependencies(ICommunicationContext arg)
        {
            resolver.AddDependencyInstance<IAmAUnitOfWorkFactory>(new UnitOfWorkFactory(), DependencyLifetime.PerRequest);
            return PipelineContinuation.Continue;
        }

    }
}