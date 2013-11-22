using OpenRasta.Configuration;
using Tasklist.Adapters.API.Resources;

namespace Tasklist
{
    public class Configuration : IConfigurationSource
    {
        public void Configure()
        {
            using (OpenRastaConfiguration.Manual)
            {
                ResourceSpace.Has.ResourcesOfType<TaskResource>()
                        .AtUri("/home")
                        .HandledBy<HomeHandler>()
                        .RenderedByAspx("~/Views/HomeView.aspx");
            }
        }
    }
}