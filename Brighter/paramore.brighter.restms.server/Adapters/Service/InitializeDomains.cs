using Microsoft.Practices.Unity;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.server.Adapters.Service
{
    internal class InitializeDomains
    {
        public static void Run(UnityContainer container)
        {
            var domainRepository = container.Resolve<IAmARepository<Domain>>();
            var feedRepository = container.Resolve<IAmARepository<Feed>>();
            new RestMSServerBuilder()
                .With()
                .Repositories(domainRepository,feedRepository)
                .Do();
        }
    }
}