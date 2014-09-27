using Microsoft.Practices.Unity;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Repositories;

namespace paramore.brighter.restms.server.Adapters.Service
{
    internal static class IoCConfiguration
    {
        public static void Run(UnityContainer container)
        {
            container.RegisterType<IAmARepository<Domain>, InMemoryDomainRepository>(new ContainerControlledLifetimeManager());
            container.RegisterType<IAmARepository<Feed>, InMemoryFeedRepository>(new ContainerControlledLifetimeManager());
        }
    }
}