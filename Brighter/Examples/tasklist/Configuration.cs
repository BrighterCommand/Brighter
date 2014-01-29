using System.Collections.Generic;
using OpenRasta.Codecs;
using OpenRasta.Configuration;
using Tasklist.Adapters.API.Contributors;
using Tasklist.Adapters.API.Handlers;
using Tasklist.Adapters.API.Resources;

namespace Tasklist
{
    public class Configuration : IConfigurationSource
    {
        public void Configure()
        {
            using (OpenRastaConfiguration.Manual)
            {
                ResourceSpace.Uses.PipelineContributor<DependencyPipelineContributor>();
                ResourceSpace.Has.ResourcesOfType<TaskModel>()
                        .AtUri("/tasks/{id}")
                        .HandledBy<TaskEndPointHandler>()
                        .TranscodedBy<JsonDataContractCodec>()
                        .ForMediaType("application/json")
                        .ForExtension("js")
                        .ForExtension("json");

                ResourceSpace.Has.ResourcesOfType<List<TaskModel>>()
                         .AtUri("/tasks")
                         .HandledBy<TaskEndPointHandler>()
                         .TranscodedBy<JsonDataContractCodec>()
                         .ForMediaType("application/json")
                         .ForExtension("js")
                         .ForExtension(".json");
            }
        }
    }
}