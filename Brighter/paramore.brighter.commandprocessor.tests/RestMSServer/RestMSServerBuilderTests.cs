using Machine.Specifications;
using paramore.brighter.restms.server.Adapters.Repositories;
using paramore.brighter.restms.server.Adapters.Service;

namespace paramore.commandprocessor.tests.RestMSServer
{
    public class When_Building_A_New_RestMS_Server
    {
        Because of = () => new RestMSServerBuilder()
            .WithRepositories(new InMemoryDomainRepository(), new InMemoryFeedRepository())
            .Do();
    }
}
