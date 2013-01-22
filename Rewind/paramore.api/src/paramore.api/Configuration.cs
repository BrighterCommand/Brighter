using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Adapters.Presentation.API.Handlers;
using Paramore.Adapters.Presentation.API.Resources;
using Paramore.Domain.Venues;

namespace Paramore.Adapters.Presentation.API
{
    public class Configuration : IConfigurationSource
    {
        public void Configure()
        {
            using (OpenRastaConfiguration.Manual)
            {
                //Dependencies
                ResourceSpace.
                    Uses.
                    CustomDependency<IAmAUnitOfWorkFactory, UnitOfWorkFactory>(DependencyLifetime.PerRequest);

                //Resources
                ResourceSpace.Has.ResourcesOfType<EntryPoint>()
                    .AtUri("/entrypoint")
                    .HandledBy<EntryPointHandler>()
                    .AsXmlDataContract()
                    .And.AsJsonDataContract();

                ResourceSpace.Has.ResourcesOfType<VenueDocument>()
                    .AtUri("/venues")
                    .HandledBy<VenueHandler>()
                    .AsXmlDataContract()
                    .And.AsJsonDataContract();
            }
        }

    }
}