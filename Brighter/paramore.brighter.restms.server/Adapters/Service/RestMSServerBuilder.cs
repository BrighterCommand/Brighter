using paramore.brighter.restms.server.Model;
using paramore.brighter.restms.server.Ports.Common;

namespace paramore.brighter.restms.server.Adapters.Service
{
    public class RestMSServerBuilder :IBuildARestMSService, IUseRepositories
    {
        #region Implementation of IBuildARestMSService

        public void Do()
        {
        }

        #endregion

        #region Implementation of IUseRepositories

        public IBuildARestMSService WithRepositories(IAmARepository<Domain> domainRepository, IAmARepository<Feed> feedRepository)
        {
        }

        #endregion
    }

    public interface IBuildARestMSService
    {
        void Do();
    }

    public interface IUseRepositories
    {
        IBuildARestMSService WithRepositories(IAmARepository<Domain> domainRepository, IAmARepository<Feed> feedRepository);
    }
}
