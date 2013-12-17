using System.Collections.Generic;
using OpenRasta.Codecs;
using OpenRasta.Configuration;
using Paramore.Adapters.Presentation.API.Contributors;
using Paramore.Adapters.Presentation.API.Handlers;
using Paramore.Adapters.Presentation.API.Resources;

namespace Paramore.Adapters.Presentation.API
{
    public class Configuration : IConfigurationSource
    {
        public void Configure()
        {
            using (OpenRastaConfiguration.Manual)
            {
                ResourceSpace.Uses.PipelineContributor<DependencyPipelineContributor>();
                ResourceSpace.Uses.PipelineContributor<CrossDomainPipelineContributor>();

                //Resources
                ResourceSpace.Has.ResourcesOfType<EntryPointResource>()
                    .AtUri("/entrypoint")
                    .HandledBy<EntryPointHandler>()
                   .AsXmlDataContract().ForMediaType("application/vnd.paramore.data+xml").ForExtension("xml")
                    .And.TranscodedBy<JsonDataContractCodec>().ForMediaType("application/vnd.paramore.data+json;q=1").ForExtension("js").ForExtension("json");

                //GET/POST /venues 
                ResourceSpace.Has.ResourcesOfType<List<VenueResource>>()
                     .AtUri("/venues")
                     .HandledBy<VenueEndPointHandler>()
                     .TranscodedBy<JsonDataContractCodec>()
                        .ForMediaType("application/vnd.paramore.data+json")
                        .ForExtension("js")
                        .ForExtension("json");
                     //.And
                     //.TranscodedBy<XmlSerializerCodec>()
                     //   .ForMediaType("application/vnd.paramore.data+xml")
                     //   .ForExtension("xml");

                //PUT /venues/id
                ResourceSpace.Has.ResourcesOfType<VenueResource>()
                     .AtUri("/venues/{id}")
                     .HandledBy<VenueEndPointHandler>()
                     .TranscodedBy<JsonDataContractCodec>()
                     .ForMediaType("application/vnd.paramore.data+json")
                     .ForExtension("js")
                     .ForExtension("json");
                //.And
                //.TranscodedBy<XmlSerializerCodec>()
                //    .ForMediaType("application/vnd.paramore.data+xml")
                //    .ForExtension("xml");

                //DELETE /venues/id

                //GET/POST /speakers
                ResourceSpace.Has.ResourcesOfType<List<SpeakerResource>>()
                     .AtUri("/speakers")
                     .HandledBy<SpeakerEndPointHandler>()
                     .TranscodedBy<JsonDataContractCodec>()
                        .ForMediaType("application/vnd.paramore.data+json")
                        .ForExtension("js")
                        .ForExtension("json");
                     //.And
                     //.TranscodedBy<XmlSerializerCodec>()
                     //   .ForMediaType("application/vnd.paramore.data+xml")
                     //   .ForExtension("xml");


            }
        }

    }
}