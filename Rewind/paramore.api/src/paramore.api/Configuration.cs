using System;
using OpenRasta.Configuration;
using OpenRasta.DI;
using Paramore.Domain.Venues;
using Paramore.Infrastructure.Repositories;
using paramore.api.Handlers;
using paramore.api.Resources;

namespace paramore.api
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