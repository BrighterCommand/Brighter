using OpenRasta.DI;
using OpenRasta.Pipeline;
using OpenRasta.Web;
using paramore.commandprocessor;
using paramore.commandprocessor.ioccontainers.IoCContainers;
using Paramore.Rewind.Core.Adapters.Repositories;
using Paramore.Rewind.Core.Domain.Venues;
using Paramore.Rewind.Core.Ports.Commands.Venue;
using Paramore.Rewind.Core.Ports.Handlers.Venues;
using TinyIoC;

namespace paramore.rewind.adapters.presentation.api.Contributors
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
            container.Register<IHandleRequests<AddVenueCommand>, AddVenueCommandHandler>().AsMultiInstance();
            container.Register<IHandleRequests<UpdateVenueCommand>, UpdateVenueCommandHandler>().AsMultiInstance();
            container.Register<IHandleRequests<DeleteVenueCommand>, DeleteVenueCommandHandler>().AsMultiInstance();
            container.Register<IRepository<Venue, VenueDocument>, Repository<Venue, VenueDocument>>().AsMultiInstance();
            container.Register<IAmAUnitOfWorkFactory, UnitOfWorkFactory>().AsSingleton();

            resolver.AddDependencyInstance<IAdaptAnInversionOfControlContainer>(container, DependencyLifetime.Singleton);
            resolver.AddDependencyInstance<IAmARequestContextFactory>(new InMemoryRequestContextFactory(), DependencyLifetime.PerRequest);
            resolver.AddDependencyInstance<IAmAUnitOfWorkFactory>(new UnitOfWorkFactory(), DependencyLifetime.Singleton);
            resolver.AddDependency<IAmACommandProcessor, CommandProcessor>(DependencyLifetime.Singleton);

            return PipelineContinuation.Continue;
        }
    }
}