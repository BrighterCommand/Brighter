using System;
using OpenRasta.Configuration;
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
                ResourceSpace.Has.ResourcesOfType<EntryPoint>()
                    .AtUri("/entrypoint")
                    .HandledBy<EntryPointHandler>()
                    .AsXmlDataContract()
                    .And.AsJsonDataContract();
            }
        }

    }
}