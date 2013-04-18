using Paramore.Adapters.Infrastructure.Repositories;
using OpenRasta.DI;
using OpenRasta.Pipeline;
using OpenRasta.Web;
using Paramore.Domain.Venues;
using Paramore.Ports.Services.Commands.Venue;
using Paramore.Ports.Services.Handlers.Venues;
using TinyIoC;
using paramore.commandprocessor;
using paramore.commandprocessor.ioccontainers.IoCContainers;

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
            pipelineRunner.Notify(InitializeContainer)
                .Before<KnownStages.IOperationExecution>();
        }

        private PipelineContinuation InitializeContainer(ICommunicationContext arg)
        {
            var container = new TinyIoCAdapter(new TinyIoCContainer());
            //HACK! For now dependencies may need to be in both containers to allow resolution
            //, IAmAUnitOfWorkFactory unitOfWorkFactory
            container.Register<IHandleRequests<AddVenueCommand>, AddVenueCommandHandler>().AsMultiInstance();
            container.Register<IRepository<Venue,VenueDocument>, Repository<Venue, VenueDocument>>().AsMultiInstance();
            container.Register<IAmAUnitOfWorkFactory, UnitOfWorkFactory>().AsSingleton();
            
            resolver.AddDependencyInstance<IAdaptAnInversionOfControlContainer>(container, DependencyLifetime.Singleton);
            resolver.AddDependencyInstance<IAmARequestContextFactory>(new InMemoryRequestContextFactory(), DependencyLifetime.PerRequest);
            resolver.AddDependencyInstance<IAmAUnitOfWorkFactory>(new UnitOfWorkFactory(), DependencyLifetime.Singleton);
            resolver.AddDependency<IAmACommandProcessor, CommandProcessor>(DependencyLifetime.Singleton);

            return PipelineContinuation.Continue;
        }
    }
}