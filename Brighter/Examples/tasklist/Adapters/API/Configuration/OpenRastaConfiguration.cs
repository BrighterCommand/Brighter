#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using OpenRasta.Codecs;
using OpenRasta.Configuration;
using OpenRasta.DI;

using Tasklist.Adapters.API.Handlers;
using Tasklist.Adapters.API.Resources;

namespace Tasklist.Adapters.API.Configuration
{
    public class OpenRastaConfiguration : IConfigurationSource, IDependencyResolverAccessor
    {
        public void Configure()
        {
            using (OpenRasta.Configuration.OpenRastaConfiguration.Manual)
            {
                ResourceSpace.Has.ResourcesOfType<TaskModel>()
                        .AtUri("/tasks/{taskId}")
                        .HandledBy<TaskEndPointHandler>()
                        .TranscodedBy<JsonDataContractCodec>()
                        .ForMediaType("application/json")
                        .ForExtension("js")
                        .ForExtension("json");

                ResourceSpace.Has.ResourcesOfType<TaskListModel>()
                         .AtUri("/tasks")
                         .HandledBy<TaskEndPointHandler>()
                         .TranscodedBy<JsonDataContractCodec>()
                         .ForMediaType("application/json")
                         .ForExtension("js")
                         .ForExtension(".json");

                ResourceSpace.Has.ResourcesOfType<TaskReminderModel>()
                    .AtUri("/tasks/reminders")
                    .HandledBy<TaskReminderEndpointHandler>()
                    .TranscodedBy<JsonDataContractCodec>()
                    .ForMediaType("application/json")
                    .ForExtension("js")
                    .ForExtension("json");
            }
        }

        public IDependencyResolver Resolver
        {
            get
            {
                var dependencyRegistrar = new DependencyRegistrar();
                dependencyRegistrar.Initialise();

                return dependencyRegistrar.GetDependencyResolver();
            }
        }
    }
}